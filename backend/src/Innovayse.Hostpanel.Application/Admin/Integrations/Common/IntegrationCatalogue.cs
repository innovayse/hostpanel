using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The one catalogue of built-in integrations: display name, category, form fields and the fields
/// that make an integration "configured". The list, detail and connection-test use cases all read
/// it, so an integration is added or changed here and nowhere else.
/// </summary>
public static class IntegrationCatalogue
{
    /// <summary>Category label of the payment gateways.</summary>
    private const string PaymentGateways = "Payment Gateways";

    /// <summary>Category label of the domain registrars.</summary>
    private const string DomainRegistrars = "Domain Registrars";

    /// <summary>Category label of the hosting and provisioning panels.</summary>
    private const string HostingProvisioning = "Hosting / Provisioning";

    /// <summary>Input kind of a plain text field.</summary>
    private const IntegrationFieldType Text = IntegrationFieldType.Text;

    /// <summary>Input kind of a masked secret field.</summary>
    private const IntegrationFieldType Password = IntegrationFieldType.Password;

    /// <summary>Input kind of a drop-down field.</summary>
    private const IntegrationFieldType Select = IntegrationFieldType.Select;

    /// <summary>Input kind of a multi-line text field.</summary>
    private const IntegrationFieldType TextArea = IntegrationFieldType.TextArea;

    /// <summary>Input kind of an on/off switch.</summary>
    private const IntegrationFieldType Toggle = IntegrationFieldType.Toggle;

    /// <summary>
    /// Integrations shown in the list and the detail form, in display order. The hosted payment
    /// gateway is deliberately absent: it appears through the plugin registry, not as a built-in.
    /// </summary>
    private static readonly Dictionary<string, IntegrationCatalogueEntry> BuiltIn = new()
    {
        [IntegrationSlugs.BankTransfer] = new("Bank Transfer", PaymentGateways,
            [
                new(BankTransferFields.AccountName, "Account Name", Text, Required: false),
                new(BankTransferFields.Iban, "IBAN", Text, Required: false),
                new(BankTransferFields.Swift, "SWIFT / BIC", Text, Required: false),
                new(BankTransferFields.BankName, "Bank Name", Text, Required: false),
                new(BankTransferFields.Instructions, "Payment Instructions", TextArea, Required: false),
                new(BankTransferFields.ExpiryDays, "Days until an unconfirmed transfer lapses", Text, Required: false),
            ],
            []),
        [IntegrationSlugs.Namecheap] = new("Namecheap", DomainRegistrars,
            [
                new("api_key", "API Key", Password, Required: true),
                new("api_username", "API Username", Text, Required: true),
                new("client_ip", "Whitelisted Client IP", Text, Required: true),
            ],
            ["api_key", "api_username", "client_ip"]),
        [IntegrationSlugs.NameAm] = new("Name.am", DomainRegistrars,
            [
                new("email", "Account Email", Text, Required: true),
                new("password", "Account Password", Password, Required: true),
                new("api_url", "API URL", Text, Required: false),
                new("test_mode", "Test Mode", Toggle, Required: false),
            ],
            ["email", "password"]),
        [IntegrationSlugs.ResellerClub] = new("ResellerClub", DomainRegistrars,
            [
                new("reseller_id", "Reseller ID", Text, Required: true),
                new("api_key", "API Key", Password, Required: true),
            ],
            ["reseller_id", "api_key"]),
        [IntegrationSlugs.Enom] = new("ENOM", DomainRegistrars,
            [
                new("account_id", "Account ID", Text, Required: true),
                new("api_key", "API Key", Password, Required: true),
            ],
            ["account_id", "api_key"]),
        [IntegrationSlugs.Cpanel] = new("cPanel WHM", HostingProvisioning,
            [
                new("host", "WHM Host", Text, Required: true),
                new("port", "Port", Text, Required: false),
                new("username", "Username", Text, Required: true),
                new("api_token", "API Token", Password, Required: true),
            ],
            ["host", "username", "api_token"]),
        [IntegrationSlugs.Plesk] = new("Plesk", HostingProvisioning,
            [
                new("host", "Plesk Host", Text, Required: true),
                new("port", "Port", Text, Required: false),
                new("username", "Username", Text, Required: true),
                new("password", "Password", Password, Required: true),
            ],
            ["host", "username", "password"]),
        [IntegrationSlugs.Cwp] = new("CentOS Web Panel", HostingProvisioning,
            [
                new("host", "CWP Host", Text, Required: true),
                new("port", "Port", Text, Required: false),
                new("api_key", "API Key", Password, Required: true),
            ],
            ["host", "api_key"]),
        [IntegrationSlugs.Cwp7] = new("CWP7", HostingProvisioning, [], []),
        [IntegrationSlugs.Smtp] = new("SMTP Server", "Email / SMTP",
            [
                new("host", "SMTP Host", Text, Required: true),
                new("port", "Port", Text, Required: false),
                new("username", "Username", Text, Required: false),
                new("password", "Password", Password, Required: false),
                new("from_address", "From Address", Text, Required: true),
                new("encryption", "Encryption", Select, Required: false, Options: ["TLS", "SSL", "None"]),
            ],
            ["host", "username", "password", "from_address"]),
        [IntegrationSlugs.MaxMind] = new("MaxMind", "Fraud Protection",
            [
                new("account_id", "Account ID", Text, Required: true),
                new("license_key", "License Key", Password, Required: true),
            ],
            ["account_id", "license_key"]),
    };

    /// <summary>Gets every built-in integration with its slug, in display order.</summary>
    public static IReadOnlyDictionary<string, IntegrationCatalogueEntry> All => BuiltIn;

    /// <summary>Looks up a built-in integration as the list and detail use cases see it.</summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="entry">The catalogue entry when found.</param>
    /// <returns><c>true</c> when the slug names a built-in integration.</returns>
    public static bool TryGet(string slug, out IntegrationCatalogueEntry entry)
    {
        return BuiltIn.TryGetValue(slug, out entry!);
    }

    /// <summary>Looks up an integration that the connection test checks by its stored fields.</summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="entry">The catalogue entry when found.</param>
    /// <returns>
    /// <c>true</c> when the slug names a built-in integration; a payment plugin is not in the
    /// catalogue and is probed through its plugin instead.
    /// </returns>
    public static bool TryGetTestable(string slug, out IntegrationCatalogueEntry entry)
    {
        return BuiltIn.TryGetValue(slug, out entry!);
    }
}
