using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Kazo.Jarvis.Adapters.Updates;
using Kazo.Jarvis.Kernel.Updates;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Updates;

[TestFixture]
public sealed class GitHubReleaseSourceTests
{
    private const string ReleasesJson = """
        [
          {
            "tag_name": "v0.2.0-beta.1",
            "name": "Kazo.Jarvis 0.2.0 beta 1",
            "draft": false,
            "prerelease": true,
            "html_url": "https://github.com/Kazo-ca/Jarvis/releases/tag/v0.2.0-beta.1",
            "assets": [
              { "name": "Kazo.Jarvis-0.2.0-beta.1-win-x64.zip", "size": 1234, "digest": "sha256:ab", "browser_download_url": "https://github.com/Kazo-ca/Jarvis/releases/download/v0.2.0-beta.1/Kazo.Jarvis-0.2.0-beta.1-win-x64.zip" },
              { "name": "broken", "browser_download_url": "not a url" }
            ]
          },
          { "tag_name": "v0.1.0", "draft": true, "html_url": "https://github.com/Kazo-ca/Jarvis/releases/tag/v0.1.0" },
          { "name": "no tag" }
        ]
        """;

    private TemporaryFolder folder = null!;

    [SetUp]
    public void SetUp() => folder = new TemporaryFolder();

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public void Reads_the_releases_of_the_github_api()
    {
        using var document = JsonDocument.Parse(ReleasesJson);

        var releases = GitHubReleaseSource.Parse(document.RootElement);

        Assert.Multiple(() =>
        {
            Assert.That(releases, Has.Count.EqualTo(2));
            Assert.That(releases[0].Tag, Is.EqualTo("v0.2.0-beta.1"));
            Assert.That(releases[0].IsPreRelease, Is.True);
            Assert.That(releases[0].Packages, Has.Count.EqualTo(1));
            Assert.That(releases[0].Packages[0].Size, Is.EqualTo(1234));
            Assert.That(releases[0].Packages[0].Digest, Is.EqualTo("sha256:ab"));
            Assert.That(releases[1].IsDraft, Is.True);
        });
    }

    [Test]
    public async Task Asks_the_public_api_of_kazo_ca_jarvis_without_a_token()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleasesJson) });
        var source = new GitHubReleaseSource(new HttpClient(handler), folder.Paths);

        var releases = await source.GetReleasesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(releases, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0].RequestUri!.ToString(), Is.EqualTo("https://api.github.com/repos/Kazo-ca/Jarvis/releases?per_page=30"));
            Assert.That(handler.Requests[0].Headers.Authorization, Is.Null);
            Assert.That(handler.Requests[0].Headers.UserAgent.ToString(), Is.Not.Empty);
        });
    }

    [Test]
    public void A_refusal_or_a_strange_answer_is_an_http_failure()
    {
        var limited = new GitHubReleaseSource(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))), folder.Paths);
        var strange = new GitHubReleaseSource(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"message\":1}") })), folder.Paths);

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<HttpRequestException>(() => limited.GetReleasesAsync());
            Assert.ThrowsAsync<HttpRequestException>(() => strange.GetReleasesAsync());
        });
    }

    [Test]
    public async Task Prepares_a_package_downloaded_checked_and_unpacked_under_updates()
    {
        var zip = Package("Kazo.Jarvis.Windows.exe", "Kazo.Jarvis.Updater.exe", "fr/Kazo.Jarvis.Features.resources.dll");
        var source = Serving(zip);
        var progress = new List<double>();

        var content = await source.PrepareAsync(Asset(zip, Digest(zip)), new SyncProgress(progress.Add));

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.StartWith(folder.Paths.Updates));
            Assert.That(File.Exists(Path.Combine(content, "Kazo.Jarvis.Updater.exe")), Is.True);
            Assert.That(File.Exists(Path.Combine(content, "fr", "Kazo.Jarvis.Features.resources.dll")), Is.True);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(content)!), Is.Empty, "the zip is deleted once unpacked");
            Assert.That(progress, Has.Count.GreaterThan(0));
            Assert.That(progress[^1], Is.EqualTo(1.0));
        });
    }

    [Test]
    public void A_file_that_does_not_match_its_digest_is_refused_and_deleted()
    {
        var zip = Package("Kazo.Jarvis.Windows.exe", "Kazo.Jarvis.Updater.exe");
        var source = Serving(zip);

        Assert.ThrowsAsync<InvalidDataException>(() => source.PrepareAsync(Asset(zip, "sha256:" + new string('0', 64))));
        Assert.That(Directory.GetFiles(folder.Paths.Updates, "*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public void A_package_without_the_updater_is_refused()
    {
        var zip = Package("Kazo.Jarvis.Windows.exe");

        Assert.ThrowsAsync<InvalidDataException>(() => Serving(zip).PrepareAsync(Asset(zip, null)));
    }

    [Test]
    public void A_file_that_is_not_a_zip_is_refused()
    {
        var notZip = "not a zip"u8.ToArray();

        Assert.ThrowsAsync<InvalidDataException>(() => Serving(notZip).PrepareAsync(Asset(notZip, null)));
    }

    [Test]
    public async Task Leftovers_of_earlier_updates_are_deleted()
    {
        var zip = Package("Kazo.Jarvis.Windows.exe", "Kazo.Jarvis.Updater.exe");
        var source = Serving(zip);
        await source.PrepareAsync(Asset(zip, null));

        source.DeleteLeftovers();

        Assert.That(Directory.GetDirectories(folder.Paths.Updates), Is.Empty);
    }

    private GitHubReleaseSource Serving(byte[] content) =>
        new(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) })), folder.Paths);

    private static ReleasePackage Asset(byte[] content, string? digest) =>
        new("Kazo.Jarvis-0.2.0-win-x64.zip", new Uri("https://example.invalid/Kazo.Jarvis-0.2.0-win-x64.zip"), content.Length, digest);

    private static string Digest(byte[] content) => "sha256:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static byte[] Package(params string[] files)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(file).Open());
                writer.Write(file);
            }
        }

        return memory.ToArray();
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    /// <summary>Reports on the calling thread, unlike <see cref="Progress{T}"/>.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
