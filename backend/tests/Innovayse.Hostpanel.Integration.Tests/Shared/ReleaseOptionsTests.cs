using System.ComponentModel.DataAnnotations;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>Unit tests for <see cref="ReleaseOptions"/> data-annotation validation.</summary>
public sealed class ReleaseOptionsTests
{
    /// <summary>The built-in defaults satisfy all constraints.</summary>
    [Fact]
    public void Defaults_AreValid()
    {
        Validate(new ReleaseOptions()).Should().BeEmpty();
    }

    /// <summary>An empty <see cref="ReleaseOptions.Repository"/> fails validation.</summary>
    [Fact]
    public void EmptyRepository_IsRefused()
    {
        var options = new ReleaseOptions { Repository = string.Empty };

        Validate(options).Should().ContainSingle();
    }

    /// <summary>A non-URL value for <see cref="ReleaseOptions.GitHubApiBaseUrl"/> fails validation.</summary>
    [Fact]
    public void BadGitHubApiBaseUrl_IsRefused()
    {
        var options = new ReleaseOptions { GitHubApiBaseUrl = "not-a-url" };

        Validate(options).Should().ContainSingle();
    }

    /// <summary>A non-URL value for <see cref="ReleaseOptions.InstallScriptUrl"/> fails validation.</summary>
    [Fact]
    public void BadInstallScriptUrl_IsRefused()
    {
        var options = new ReleaseOptions { InstallScriptUrl = "not-a-url" };

        Validate(options).Should().ContainSingle();
    }

    /// <summary>Runs the full data-annotation validation pass on the supplied options.</summary>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>The list of validation failures found.</returns>
    private static List<ValidationResult> Validate(ReleaseOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
