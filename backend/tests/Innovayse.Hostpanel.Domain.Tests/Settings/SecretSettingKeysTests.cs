using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="SecretSettingKeys"/>: which setting keys are write only. A key wrongly
/// classified as plain is sent to the browser, so the cases cover every credential the built-in
/// integrations and plugins store, and the public keys that must stay readable.
/// </summary>
public class SecretSettingKeysTests
{
    /// <summary>Every credential field of the payment gateways, registrars, panels, SMTP and plugins is secret.</summary>
    /// <param name="key">The full setting key.</param>
    [Theory]
    [InlineData("integration:stripe:secret_key")]
    [InlineData("integration:stripe:webhook_secret")]
    [InlineData("integration:paypal:client_secret")]
    [InlineData("integration:namecheap:api_key")]
    [InlineData("integration:nameam:password")]
    [InlineData("integration:cpanel:api_token")]
    [InlineData("integration:smtp:password")]
    [InlineData("integration:maxmind:license_key")]
    [InlineData("integration:cwp7:access_hash")]
    [InlineData("integration:inecobank:password")]
    [InlineData("INTEGRATION:Stripe:SECRET_KEY")]
    [InlineData(SecretSettingKeys.SetupToken)]
    public void IsSecret_CredentialKeys_AreSecret(string key)
    {
        Assert.True(SecretSettingKeys.IsSecret(key));
    }

    /// <summary>Identifiers, hosts, flags and public widget tokens stay readable.</summary>
    /// <param name="key">The full setting key.</param>
    [Theory]
    [InlineData("integration:stripe:publishable_key")]
    [InlineData("integration:stripe:is_enabled")]
    [InlineData("integration:paypal:client_id")]
    [InlineData("integration:nameam:email")]
    [InlineData("integration:cpanel:host")]
    [InlineData("integration:cwp7:hostname")]
    [InlineData(PortalSettingKeys.ChatWebsiteToken)]
    [InlineData(PortalSettingKeys.SiteName)]
    [InlineData("password")]
    [InlineData("integration:password")]
    public void IsSecret_PlainKeys_AreNotSecret(string key)
    {
        Assert.False(SecretSettingKeys.IsSecret(key));
    }

    /// <summary>No key the storefront reads anonymously is a secret.</summary>
    [Fact]
    public void Public_NamesNoSecret()
    {
        foreach (var key in PortalSettingKeys.Public)
        {
            Assert.False(SecretSettingKeys.IsSecret(key), key);
        }
    }

    /// <summary>The manifest type and the catalogue type both mark a secret field; other types do not.</summary>
    [Fact]
    public void IsSecretFieldType_RecognisesBothSecretTypes()
    {
        Assert.True(SecretSettingKeys.IsSecretFieldType(IntegrationFieldType.Secret));
        Assert.True(SecretSettingKeys.IsSecretFieldType(IntegrationFieldType.Password));
        Assert.False(SecretSettingKeys.IsSecretFieldType(IntegrationFieldType.Text));
        Assert.False(SecretSettingKeys.IsSecretFieldType(IntegrationFieldType.TextArea));
    }
}
