using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// One type per file, the file named after the type, the folder equal to the namespace
/// (rules/file-layout.md). Read from the sources under <c>backend/src</c>, since a compiled
/// assembly no longer knows which file a type came from.
/// </summary>
public sealed partial class SourceLayoutTests
{
    /// <summary>The file holding top-level statements, which C# does not let declare a namespace.</summary>
    private const string EntryPointFile = "Program.cs";

    /// <summary>The suffix of a file holding the generic twin of a non-generic type, such as <c>ResultOfT.cs</c>.</summary>
    private const string GenericTwinSuffix = "OfT";

    /// <summary>
    /// Folders that are never scanned: build output, the migrations <c>dotnet ef</c> generates,
    /// whose file names carry a timestamp the class name does not, and <c>tests</c>, the SDK's own test
    /// project inside its submodule, which keeps the layout of its own repository.
    /// </summary>
    private static readonly string[] SkippedFolders = ["bin", "obj", "Migrations", "tests"];

    /// <summary>A file-scoped namespace declaration.</summary>
    /// <returns>The regex; group <c>name</c> is the namespace.</returns>
    [GeneratedRegex(@"^namespace\s+(?<name>[\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex FileScopedNamespace();

    /// <summary>
    /// A top-level type declaration: at column 0, as every type under a file-scoped namespace is,
    /// after optional attributes and modifiers.
    /// </summary>
    /// <returns>The regex; group <c>name</c> is the type name, group <c>generic</c> is set for a generic type.</returns>
    [GeneratedRegex(@"^(?:\[[^\]\r\n]*\]\s*)*(?:(?:public|internal|static|sealed|abstract|partial|readonly|file|ref|unsafe)\s+)*(?:class|record|struct|interface|enum)\s+(?:struct\s+|class\s+)?(?<name>[A-Za-z_]\w*)(?<generic><)?", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    /// <summary>Every source file declares at most one top-level type.</summary>
    [Fact]
    public void EveryFile_DeclaresOneType()
    {
        var offenders = SourceFiles()
            .Where(file => TopLevelType().Matches(file.Text).Count > 1)
            .Select(file => file.Relative)
            .ToList();

        offenders.Should().BeEmpty("each type gets its own file");
    }

    /// <summary>The one type a file declares gives the file its name.</summary>
    [Fact]
    public void EveryFile_IsNamedAfterItsType()
    {
        var offenders = SourceFiles()
            .Select(file => (file, matches: TopLevelType().Matches(file.Text)))
            .Where(pair => pair.matches.Count == 1 && !IsNamedAfter(pair.file.Path, pair.matches[0]))
            .Select(pair => $"{pair.file.Relative} declares {pair.matches[0].Groups["name"].Value}")
            .ToList();

        offenders.Should().BeEmpty("a file is named after the type it declares");
    }

    /// <summary>Every file that declares a type has a file-scoped namespace equal to its folder.</summary>
    [Fact]
    public void EveryNamespace_MatchesItsFolder()
    {
        var offenders = SourceFiles()
            .Where(file => TopLevelType().IsMatch(file.Text) && Path.GetFileName(file.Path) != EntryPointFile)
            .Select(file => (file, declared: FileScopedNamespace().Match(file.Text)))
            .Where(pair => !pair.declared.Success || pair.declared.Groups["name"].Value != pair.file.ExpectedNamespace)
            .Select(pair => $"{pair.file.Relative}: {(pair.declared.Success ? pair.declared.Groups["name"].Value : "no file-scoped namespace")} != {pair.file.ExpectedNamespace}")
            .ToList();

        offenders.Should().BeEmpty("the folder is the namespace");
    }

    /// <summary>The scan finds the sources; a rule over no files would pass for the wrong reason.</summary>
    [Fact]
    public void TheScan_FindsTheSources()
    {
        SourceFiles().Should().HaveCountGreaterThan(500);
    }

    /// <summary>Whether a file is named after its one declared type.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="declaration">The type declaration.</param>
    /// <returns><c>true</c> when the file name is the type name, or its generic twin name.</returns>
    private static bool IsNamedAfter(string path, Match declaration)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var typeName = declaration.Groups["name"].Value;

        return fileName == typeName
            || (declaration.Groups["generic"].Success && fileName == typeName + GenericTwinSuffix);
    }

    /// <summary>Every C# source file under each project in <c>backend/src</c>, with the namespace its folder implies.</summary>
    /// <returns>The files.</returns>
    private static IReadOnlyList<SourceFile> SourceFiles()
    {
        var sourceRoot = Path.Combine(UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory), "src");

        var projects = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(project => !IsSkipped(sourceRoot, project))
            .ToList();
        var projectRoots = projects.Select(project => Path.GetDirectoryName(project)!).ToList();

        return projects
            .SelectMany(project =>
            {
                var projectRoot = Path.GetDirectoryName(project)!;
                var rootNamespace = Path.GetFileNameWithoutExtension(project);

                return Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !IsSkipped(projectRoot, path) && !IsInNestedProject(projectRoots, projectRoot, path))
                    .Select(path => new SourceFile(
                        path,
                        Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'),
                        ExpectedNamespace(rootNamespace, projectRoot, path),
                        File.ReadAllText(path)));
            })
            .ToList();
    }

    /// <summary>Whether a path sits inside a skipped folder below a root.</summary>
    /// <param name="root">The root the path is relative to.</param>
    /// <param name="path">The path.</param>
    /// <returns><c>true</c> when any folder on the way is skipped.</returns>
    private static bool IsSkipped(string root, string path)
    {
        var folders = Path.GetRelativePath(root, Path.GetDirectoryName(path)!).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return folders.Any(folder => SkippedFolders.Contains(folder, StringComparer.Ordinal));
    }

    /// <summary>
    /// Whether a file belongs to a project that sits inside another project's folder (the SDK's packaging project below the SDK),
    /// so the file is read once, under the project that compiles it, and not again under the outer one.
    /// </summary>
    /// <param name="projectRoots">The folder of every scanned project.</param>
    /// <param name="projectRoot">The folder of the project being read.</param>
    /// <param name="path">The file.</param>
    /// <returns><c>true</c> when a deeper project folder than <paramref name="projectRoot"/> contains the file.</returns>
    private static bool IsInNestedProject(IReadOnlyList<string> projectRoots, string projectRoot, string path)
    {
        return projectRoots.Any(other => other.Length > projectRoot.Length
            && other.StartsWith(projectRoot, StringComparison.Ordinal)
            && path.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    /// <summary>The namespace a file's folder implies: the project name, then each folder below the project.</summary>
    /// <param name="rootNamespace">The project's root namespace, its file name.</param>
    /// <param name="projectRoot">The project folder.</param>
    /// <param name="path">The file.</param>
    /// <returns>The expected namespace.</returns>
    private static string ExpectedNamespace(string rootNamespace, string projectRoot, string path)
    {
        var relative = Path.GetRelativePath(projectRoot, Path.GetDirectoryName(path)!);

        return relative == "."
            ? rootNamespace
            : $"{rootNamespace}.{relative.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.')}";
    }
}
