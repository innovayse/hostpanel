using System.Xml.Linq;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Pins the plugin SDK's package metadata. The maintainers licensed the SDK under MIT while the rest of
/// the repository is AGPL-3.0-or-later, so a plugin compiled against it is its author's to license; a
/// package that named another licence, or shipped without the licence text or the readme, would tell
/// plugin authors something else.
/// </summary>
public sealed class SdkPackageTests
{
    /// <summary>The SDK project's folder, found from the solution root (a git submodule of its own repository).</summary>
    private static readonly string SdkRoot = Path.Combine(
        UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory), "src", "Innovayse.SDK");

    /// <summary>The package licence is the MIT expression and no licence file overrides it.</summary>
    [Fact]
    public void Package_IsMitLicensed()
    {
        var project = LoadProject();

        Property(project, "PackageLicenseExpression").Should().Be("MIT");
        Property(project, "PackageLicenseFile").Should().BeNull();
    }

    /// <summary>The MIT text sits beside the project and is packed at the package root.</summary>
    [Fact]
    public void Package_CarriesTheMitLicenseText()
    {
        var license = File.ReadAllText(Path.Combine(SdkRoot, "LICENSE"));

        license.Should().StartWith("MIT License");
        license.Should().Contain("the Hostpanel contributors");
        PackedFiles(LoadProject()).Should().Contain("LICENSE");
    }

    /// <summary>The readme is the package readme and is packed, so the feed shows how to use the package.</summary>
    [Fact]
    public void Package_CarriesTheReadme()
    {
        var project = LoadProject();

        Property(project, "PackageReadmeFile").Should().Be("README.md");
        PackedFiles(project).Should().Contain("README.md");
        File.ReadAllText(Path.Combine(SdkRoot, "README.md")).Should().Contain("dotnet add package Innovayse.SDK --version");
    }

    /// <summary>The metadata a feed shows is filled in and points at the SDK's own public repository.</summary>
    [Fact]
    public void Package_MetadataIsComplete()
    {
        var project = LoadProject();

        Property(project, "PackageId").Should().Be("Innovayse.SDK");
        Property(project, "Version").Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        Property(project, "Description").Should().NotBeNullOrWhiteSpace();
        Property(project, "PackageTags").Should().NotBeNullOrWhiteSpace();
        Property(project, "RepositoryUrl").Should().Be("https://github.com/innovayse/hostpanel-sdk.git");
        Property(project, "PackageProjectUrl").Should().Be("https://github.com/innovayse/hostpanel-sdk");
        Property(project, "IsPackable").Should().Be("true");
    }

    /// <summary>Reads the SDK project file.</summary>
    /// <returns>The parsed project.</returns>
    private static XDocument LoadProject()
    {
        return XDocument.Load(Path.Combine(SdkRoot, "Innovayse.SDK.csproj"));
    }

    /// <summary>The value of a project property, or null when the project does not set it.</summary>
    /// <param name="project">The parsed project.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The trimmed value, or null.</returns>
    private static string? Property(XDocument project, string name)
    {
        return project.Descendants(name).Select(element => element.Value.Trim()).FirstOrDefault();
    }

    /// <summary>The files the project packs, by their <c>Include</c> path.</summary>
    /// <param name="project">The parsed project.</param>
    /// <returns>Every item marked <c>Pack="true"</c>.</returns>
    private static IEnumerable<string> PackedFiles(XDocument project)
    {
        return project.Descendants()
            .Where(element => string.Equals((string?)element.Attribute("Pack"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);
    }
}
