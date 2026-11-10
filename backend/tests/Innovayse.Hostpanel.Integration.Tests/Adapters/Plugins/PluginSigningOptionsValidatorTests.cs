using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginSigningOptionsValidator"/> stops the start when a trusted signing key is not usable (a bad key id,
/// a public key that is not 32 bytes of base64, a key listed twice), and accepts the default empty lists.
/// </summary>
public sealed class PluginSigningOptionsValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly PluginSigningOptionsValidator _validator = new();

    /// <summary>The default options, with no keys, are valid: a host with no maintainer key yet still starts.</summary>
    [Fact]
    public void TheDefaultOptions_AreValid()
    {
        _validator.Validate(null, new PluginSigningOptions()).Succeeded.Should().BeTrue();
    }

    /// <summary>Keys in both lists are valid.</summary>
    [Fact]
    public void KeysInBothLists_AreValid()
    {
        var options = new PluginSigningOptions
        {
            MaintainerKeys = [PluginSigningTestSeeds.TrustedEntry],
            OperatorKeys = [new TrustedPluginKey { KeyId = "second", PublicKey = Convert.ToBase64String(PluginSigningTestSeeds.UntrustedKey.PublicKey) }],
        };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// An operator entry with a blank key id and a blank public key, which an unset compose variable pair produces, is
    /// valid and is not part of the trusted set.
    /// </summary>
    [Fact]
    public void ABlankOperatorEntry_IsValidAndIgnored()
    {
        var options = new PluginSigningOptions
        {
            MaintainerKeys = [PluginSigningTestSeeds.TrustedEntry],
            OperatorKeys = [new TrustedPluginKey { KeyId = string.Empty, PublicKey = " " }],
        };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
        options.TrustedKeys.Should().ContainSingle().Which.KeyId.Should().Be(PluginSigningTestSeeds.TrustedKeyId);
    }

    /// <summary>An entry with only the key id set, or only the public key set, still fails: half a key is a mistake.</summary>
    /// <param name="keyId">The key id, blank or set.</param>
    /// <param name="publicKey">The public key, blank or set.</param>
    [Theory]
    [InlineData("operator-one", "")]
    [InlineData("", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void AHalfSetOperatorEntry_Fails(string keyId, string publicKey)
    {
        var options = new PluginSigningOptions { OperatorKeys = [new TrustedPluginKey { KeyId = keyId, PublicKey = publicKey }] };

        _validator.Validate(null, options).Failed.Should().BeTrue();
    }

    /// <summary>A key id outside the allowed shape fails, naming the variable to fix.</summary>
    /// <param name="keyId">The bad key id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("-leading")]
    [InlineData("line\nfeed")]
    public void ABadKeyId_Fails(string keyId)
    {
        var options = new PluginSigningOptions
        {
            OperatorKeys = [new TrustedPluginKey { KeyId = keyId, PublicKey = Convert.ToBase64String(PluginSigningTestSeeds.TrustedKey.PublicKey) }],
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PluginSigning__OperatorKeys__<n>__KeyId");
    }

    /// <summary>A public key that is not the base64 of 32 bytes fails.</summary>
    /// <param name="publicKey">The bad public key.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void ABadPublicKey_Fails(string publicKey)
    {
        var options = new PluginSigningOptions { OperatorKeys = [new TrustedPluginKey { KeyId = "key-one", PublicKey = publicKey }] };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PublicKey").And.Contain("key-one");
    }

    /// <summary>The same key id in the maintainers' list and the operator's list fails: an id names one key.</summary>
    [Fact]
    public void ADuplicateKeyId_Fails()
    {
        var options = new PluginSigningOptions
        {
            MaintainerKeys = [PluginSigningTestSeeds.TrustedEntry],
            OperatorKeys = [PluginSigningTestSeeds.TrustedEntry],
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(PluginSigningTestSeeds.TrustedKeyId);
    }
}
