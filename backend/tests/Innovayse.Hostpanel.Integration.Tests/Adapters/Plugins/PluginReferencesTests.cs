using System.Security.Cryptography;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginReferences"/>: a reference is stable for one plugin and one subject, differs for every other
/// plugin, subject and kind, is not the host's id, and depends on the deployment's key.
/// </summary>
public sealed class PluginReferencesTests
{
    /// <summary>A key for the tests: 32 bytes, Base64, as the host's encryption key is.</summary>
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>The same plugin and client give the same reference every time, in a second instance built from the same key too.</summary>
    [Fact]
    public void ForClient_IsStable()
    {
        var first = new PluginReferences(Key).ForClient("crm", 42);

        first.Should().Be(new PluginReferences(Key).ForClient("crm", 42));
        first.Should().Be(new PluginReferences(Key).ForClient("crm", 42));
    }

    /// <summary>Another plugin, another client and another user each give another reference.</summary>
    [Fact]
    public void References_DifferByPluginAndBySubject()
    {
        var references = new PluginReferences(Key);

        references.ForClient("crm", 42).Should().NotBe(references.ForClient("erp", 42));
        references.ForClient("crm", 42).Should().NotBe(references.ForClient("crm", 43));
        references.ForUser("crm", "user-1").Should().NotBe(references.ForUser("erp", "user-1"));
        references.ForUser("crm", "user-1").Should().NotBe(references.ForUser("crm", "user-2"));
    }

    /// <summary>A client and a user that share a text never share a reference, because the kind is part of what is hashed.</summary>
    [Fact]
    public void AClientAndAUserWithTheSameText_GetDifferentReferences()
    {
        var references = new PluginReferences(Key);

        references.ForClient("crm", 42).Should().NotBe(references.ForUser("crm", "42"));
    }

    /// <summary>A reference is 43 URL-safe characters and shows neither the id nor the plugin it was made from.</summary>
    [Fact]
    public void AReference_IsOpaque()
    {
        var reference = new PluginReferences(Key).ForClient("crm", 424242);

        reference.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        reference.Should().NotContain("424242").And.NotContain("crm");
    }

    /// <summary>A deployment with another key gets other references: a plugin cannot compute one without the host's key.</summary>
    [Fact]
    public void AnotherKey_GivesOtherReferences()
    {
        var other = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        new PluginReferences(Key).ForClient("crm", 42).Should().NotBe(new PluginReferences(other).ForClient("crm", 42));
    }

    /// <summary>The derived key is not the encryption key: a reference is not the plain HMAC of the same text under it.</summary>
    [Fact]
    public void TheKeyIsDerived_NotTheEncryptionKeyItself()
    {
        var plain = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(Key), "client\ncrm\n42"u8.ToArray()))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        new PluginReferences(Key).ForClient("crm", 42).Should().NotBe(plain);
    }

    /// <summary>A missing key or an empty plugin id or subject is refused rather than hashed.</summary>
    [Fact]
    public void Arguments_AreChecked()
    {
        var references = new PluginReferences(Key);

        var noKey = () => new PluginReferences(" ");
        var noPlugin = () => references.ForClient(string.Empty, 1);
        var noUser = () => references.ForUser("crm", string.Empty);

        noKey.Should().Throw<ArgumentException>();
        noPlugin.Should().Throw<ArgumentException>();
        noUser.Should().Throw<ArgumentException>();
    }
}
