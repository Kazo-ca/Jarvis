namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>Locates the repository and its projects from the test output folder.</summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    /// <summary>Absolute path of the folder that holds <c>Kazo.Jarvis.slnx</c>.</summary>
    public static string Root => RootPath.Value;

    /// <summary>Absolute path of <c>src/</c>.</summary>
    public static string Src => Path.Combine(Root, "src");

    /// <summary>Absolute path of a product project folder, e.g. <c>Kazo.Jarvis.Kernel</c>.</summary>
    public static string ProjectFolder(string projectName) => Path.Combine(Src, projectName);

    /// <summary>Absolute path of a product project file, e.g. <c>Kazo.Jarvis.Kernel</c>.</summary>
    public static string ProjectFile(string projectName) =>
        Path.Combine(ProjectFolder(projectName), projectName + ".csproj");

    /// <summary>Path relative to the repository root, with forward slashes, for readable messages.</summary>
    public static string Relative(string path) =>
        Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>Source files under <paramref name="folder"/>, build outputs excluded.</summary>
    public static IEnumerable<string> SourceFiles(string folder, string searchPattern) =>
        Directory.EnumerateFiles(folder, searchPattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path)
    {
        var segments = Relative(path).Split('/');
        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Kazo.Jarvis.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Kazo.Jarvis.slnx not found above {AppContext.BaseDirectory}: run the tests from the repository.");
    }
}
