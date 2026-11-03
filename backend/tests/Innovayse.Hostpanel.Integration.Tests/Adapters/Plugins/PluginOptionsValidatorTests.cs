using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary><see cref="PluginOptionsValidator"/> refuses plugin settings that would leave the limits or the trusted list unusable.</summary>
public sealed class PluginOptionsValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly PluginOptionsValidator _validator = new();

    /// <summary>The defaults are usable: an install works on a host that sets nothing.</summary>
    [Fact]
    public void Defaults_Pass()
    {
        _validator.Validate(null, new PluginOptions()).Succeeded.Should().BeTrue();
    }

    /// <summary>A listed digest of the right shape is accepted, upper or lower case.</summary>
    [Fact]
    public void AWellFormedDigest_Passes()
    {
        var options = new PluginOptions { TrustedPackageSha256 = [new string('a', 64), new string('F', 64)] };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    /// <summary>A zero or negative entry limit refuses startup.</summary>
    [Fact]
    public void ANonPositiveEntryLimit_Fails()
    {
        var result = _validator.Validate(null, new PluginOptions { MaxEntryCount = 0 });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PluginOptions.MaxEntryCount));
    }

    /// <summary>A zero or negative size limit refuses startup.</summary>
    [Fact]
    public void ANonPositiveSizeLimit_Fails()
    {
        var result = _validator.Validate(null, new PluginOptions { MaxUncompressedBytes = -1 });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PluginOptions.MaxUncompressedBytes));
    }

    /// <summary>A view size limit below one byte or above the ceiling refuses startup.</summary>
    /// <param name="maxViewBytes">The out-of-range limit.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(PluginOptions.MaxViewBytesCeiling + 1)]
    public void AViewSizeLimitOutOfRange_Fails(long maxViewBytes)
    {
        var result = _validator.Validate(null, new PluginOptions { MaxViewBytes = maxViewBytes });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PluginOptions.MaxViewBytes));
    }

    /// <summary>The smallest and the largest allowed view size limits are accepted.</summary>
    /// <param name="maxViewBytes">The limit at an edge of the range.</param>
    [Theory]
    [InlineData(1L)]
    [InlineData(PluginOptions.MaxViewBytesCeiling)]
    public void AViewSizeLimitAtTheEdges_Passes(long maxViewBytes)
    {
        _validator.Validate(null, new PluginOptions { MaxViewBytes = maxViewBytes }).Succeeded.Should().BeTrue();
    }

    /// <summary>A trusted-list entry that is not a 64-character hex digest refuses startup.</summary>
    /// <param name="digest">The malformed entry.</param>
    [Theory]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void AMalformedDigest_Fails(string digest)
    {
        var result = _validator.Validate(null, new PluginOptions { TrustedPackageSha256 = [digest] });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PluginOptions.TrustedPackageSha256));
    }

    /// <summary>An upload folder inside the build's own plugin folder refuses startup: it would make uploads look built-in.</summary>
    [Fact]
    public void AnUploadFolderInsideTheBuiltInFolder_Fails()
    {
        var result = _validator.Validate(
            null, new PluginOptions { InstallPath = Path.Combine(PluginOptions.BuiltInRoot, "uploads") });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PluginOptions.InstallPath));
    }

    /// <summary>A migration time limit outside 1 to 3600 seconds refuses startup, naming the key.</summary>
    /// <param name="seconds">The configured limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(PluginOptions.MaxMigrationTimeoutSeconds + 1)]
    public void AMigrationTimeoutOutOfRange_Fails(int seconds)
    {
        var result = _validator.Validate(null, new PluginOptions { MigrationTimeoutSeconds = seconds });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PluginOptions.MigrationTimeoutSeconds));
    }

    /// <summary>The ends of the allowed migration time limit pass.</summary>
    /// <param name="seconds">The configured limit.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(PluginOptions.MaxMigrationTimeoutSeconds)]
    public void AMigrationTimeoutInRange_Passes(int seconds)
    {
        _validator.Validate(null, new PluginOptions { MigrationTimeoutSeconds = seconds }).Succeeded.Should().BeTrue();
    }
}
