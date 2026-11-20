using System.Formats.Tar;
using System.Net;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.BZip2;
using Kazo.Jarvis.Adapters.Models;
using Kazo.Jarvis.Kernel.Models;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Models;

/// <summary>
/// The model downloads (ADR 0006, section 7): checked by size and checksum, written aside then moved into place, the
/// sherpa-onnx archives unpacked, nothing left behind on failure.
/// </summary>
[TestFixture]
public sealed class HttpModelInstallerTests
{
    private static readonly Uri Source = new("https://models.test/file.bin");

    private TemporaryFolder folder = null!;
    private string models = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        models = Path.Combine(folder.Path, "models");
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task A_file_with_the_right_checksum_is_put_in_place_with_its_progress()
    {
        var content = Content(200_000);
        var installer = Installer(_ => Ok(content));
        var download = new ModelDownload(Source, "whisper/ggml-base.bin", content.Length, Sha256(content));
        var progress = new List<long>();

        Assert.That(installer.IsInstalled(download), Is.False);
        await installer.InstallAsync(download, new Recorder(progress));

        Assert.Multiple(() =>
        {
            Assert.That(installer.IsInstalled(download), Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(models, "whisper", "ggml-base.bin")), Is.EqualTo(content));
            Assert.That(progress, Is.Not.Empty);
            Assert.That(progress[^1], Is.EqualTo(content.Length));
            Assert.That(Staging(), Is.Empty);
        });
    }

    [Test]
    public async Task A_sha1_checksum_is_accepted_too()
    {
        var content = Content(1000);
        var installer = Installer(_ => Ok(content));
        var download = new ModelDownload(Source, "whisper/ggml-small.bin", content.Length, "sha1:" + Convert.ToHexStringLower(SHA1.HashData(content)));

        await installer.InstallAsync(download, null);

        Assert.That(installer.IsInstalled(download), Is.True);
    }

    [Test]
    public void A_wrong_checksum_deletes_the_file_and_says_so()
    {
        var content = Content(1000);
        var installer = Installer(_ => Ok(content));
        var download = new ModelDownload(Source, "vad/silero_vad.onnx", content.Length, "sha256:" + new string('0', 64));

        var failure = Assert.ThrowsAsync<ModelDownloadException>(() => installer.InstallAsync(download, null));

        Assert.Multiple(() =>
        {
            Assert.That(failure!.Failure, Is.EqualTo(ModelDownloadFailure.ChecksumMismatch));
            Assert.That(installer.IsInstalled(download), Is.False);
            Assert.That(Staging(), Is.Empty);
        });
    }

    [Test]
    public void A_wrong_size_is_refused_before_the_checksum()
    {
        var content = Content(1000);
        var installer = Installer(_ => Ok(content));
        var download = new ModelDownload(Source, "vad/silero_vad.onnx", 999, Sha256(content));

        var failure = Assert.ThrowsAsync<ModelDownloadException>(() => installer.InstallAsync(download, null));

        Assert.That(failure!.Failure, Is.EqualTo(ModelDownloadFailure.ChecksumMismatch));
    }

    [Test]
    public void A_refused_or_unreachable_server_is_named()
    {
        var notFound = Installer(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var down = Installer(_ => throw new HttpRequestException("connection refused"));
        var download = new ModelDownload(Source, "vad/silero_vad.onnx", 10, Sha256(new byte[10]));

        Assert.Multiple(() =>
        {
            Assert.That(Assert.ThrowsAsync<ModelDownloadException>(() => notFound.InstallAsync(download, null))!.Failure, Is.EqualTo(ModelDownloadFailure.Unreachable));
            Assert.That(Assert.ThrowsAsync<ModelDownloadException>(() => down.InstallAsync(download, null))!.Failure, Is.EqualTo(ModelDownloadFailure.Unreachable));
        });
    }

    [Test]
    public async Task A_good_file_replaces_an_older_one_and_a_bad_one_never_does()
    {
        var target = Path.Combine(models, "vad", "silero_vad.onnx");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "old");
        var content = Content(500);
        var download = new ModelDownload(Source, "vad/silero_vad.onnx", content.Length, Sha256(content));

        Assert.ThrowsAsync<ModelDownloadException>(() => Installer(_ => Ok(Content(500, seed: 9))).InstallAsync(download, null));
        Assert.That(File.ReadAllText(target), Is.EqualTo("old"));

        await Installer(_ => Ok(content)).InstallAsync(download, null);
        Assert.That(File.ReadAllBytes(target), Is.EqualTo(content));
    }

    [Test]
    public async Task An_archive_is_unpacked_into_its_folder_and_not_kept()
    {
        var archive = TarBz2(("vits-piper-fr_FR-tom-medium/fr_FR-tom-medium.onnx", "model"), ("vits-piper-fr_FR-tom-medium/espeak-ng-data/fr_dict", "dict"));
        var installer = Installer(_ => Ok(archive));
        var download = new ModelDownload(Source, "vits-piper-fr_FR-tom-medium", archive.Length, Sha256(archive), ModelDownloadFormat.TarBz2);

        await installer.InstallAsync(download, null);

        var voice = Path.Combine(models, "vits-piper-fr_FR-tom-medium");
        Assert.Multiple(() =>
        {
            Assert.That(installer.IsInstalled(download), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(voice, "fr_FR-tom-medium.onnx")), Is.EqualTo("model"));
            Assert.That(File.ReadAllText(Path.Combine(voice, "espeak-ng-data", "fr_dict")), Is.EqualTo("dict"));
            Assert.That(Staging(), Is.Empty);
        });
    }

    [Test]
    public void An_archive_without_its_folder_is_refused()
    {
        var archive = TarBz2(("something-else/file", "x"));
        var installer = Installer(_ => Ok(archive));
        var download = new ModelDownload(Source, "vits-piper-fr_FR-tom-medium", archive.Length, Sha256(archive), ModelDownloadFormat.TarBz2);

        var failure = Assert.ThrowsAsync<ModelDownloadException>(() => installer.InstallAsync(download, null));

        Assert.Multiple(() =>
        {
            Assert.That(failure!.Failure, Is.EqualTo(ModelDownloadFailure.Failed));
            Assert.That(installer.IsInstalled(download), Is.False);
            Assert.That(Staging(), Is.Empty);
        });
    }

    [TestCase("../outside.bin")]
    [TestCase("whisper/../../outside.bin")]
    [TestCase("/etc/outside.bin")]
    [TestCase("")]
    public void A_target_outside_the_models_folder_is_refused(string target)
    {
        var installer = Installer(_ => Ok([1]));

        Assert.Throws<ArgumentException>(() => installer.IsInstalled(new ModelDownload(Source, target, 1, Sha256([1]))));
    }

    [Test]
    public void A_cancelled_download_leaves_nothing()
    {
        using var cancel = new CancellationTokenSource();
        var content = Content(300_000);
        var installer = Installer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CancellingStream(content, cancel)) });
        var download = new ModelDownload(Source, "whisper/ggml-base.bin", content.Length, Sha256(content));

        Assert.CatchAsync<OperationCanceledException>(() => installer.InstallAsync(download, null, cancel.Token));

        Assert.Multiple(() =>
        {
            Assert.That(installer.IsInstalled(download), Is.False);
            Assert.That(Staging(), Is.Empty);
        });
    }

    [TestCase("md5:00")]
    [TestCase("sha256:abcd")]
    [TestCase("nothing")]
    public void A_malformed_checksum_is_refused(string checksum) =>
        Assert.Throws<ArgumentException>(() => HttpModelInstaller.ParseChecksum(checksum));

    private HttpModelInstaller Installer(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new HttpClient(new FakeHandler(answer)), models, null);

    private string[] Staging()
    {
        var staging = Path.Combine(models, HttpModelInstaller.StagingFolderName);
        return Directory.Exists(staging) ? Directory.GetFileSystemEntries(staging) : [];
    }

    private static HttpResponseMessage Ok(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private static byte[] Content(int length, int seed = 1)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static string Sha256(byte[] content) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(content));

    private static byte[] TarBz2(params (string Path, string Text)[] files)
    {
        using var tar = new MemoryStream();
        using (var writer = new TarWriter(tar, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var (path, text) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)) };
                writer.WriteEntry(entry);
            }
        }

        tar.Position = 0;
        using var output = new MemoryStream();
        BZip2.Compress(tar, output, isStreamOwner: false, level: 9);
        return output.ToArray();
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private sealed class Recorder(List<long> values) : IProgress<long>
    {
        public void Report(long value) => values.Add(value);
    }

    /// <summary>Gives half of the content, then cancels the download, like a user pressing Stop.</summary>
    private sealed class CancellingStream(byte[] content, CancellationTokenSource cancel) : MemoryStream(content)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position > Length / 2)
            {
                await cancel.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
