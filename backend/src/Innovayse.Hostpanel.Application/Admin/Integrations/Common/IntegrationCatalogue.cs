using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The one catalogue of built-in integrations: display name, category, form fields and the fields
/// that make an integration "configured". The list, detail and connection-test use cases all read
/// it, so an integration is added or changed here and nowhere else. Every text the admin shows (name,
/// field label, option label) is an admin locale key, never a sentence: the admin owns the wording of the
/// built-in integrations and translates the key in the operator's language. The category is the stable
/// name the admin groups by, not display text.
/// </summary>
public static class IntegrationCatalogue
{
    /// <summary>Category label of the payment gateways.</summary>
    private const string PaymentGateways = "Payment Gateways";

    /// <summary>Category label of the hosting and provisioning panels.</summary>
    private const string HostingProvisioning = "Hosting / Provisioning";

    /// <summary>Locale key of the label of a port field, shared by the integrations that have one.</summary>
    private const string PortLabel = "integrationMeta.cpanel.fieldPort";

    /// <summary>Locale key of the label of a user name field, shared by the integrations that have one.</summary>
    private const string UsernameLabel = "shared.username";

    /// <summary>Locale key of the label of a password field.</summary>
    private const string PasswordLabel = "shared.password";

    /// <summary>Input kind of a plain text field.</summary>
    private const IntegrationFieldType Text = IntegrationFieldType.Text;

    /// <summary>Input kind of a masked secret field.</summary>
    private const IntegrationFieldType Password = IntegrationFieldType.Password;

    /// <summary>Input kind of a drop-down field.</summary>
    private const IntegrationFieldType Select = IntegrationFieldType.Select;

    /// <summary>Input kind of a multi-line text field.</summary>
    private const IntegrationFieldType TextArea = IntegrationFieldType.TextArea;

    /// <summary>
    /// Integrations shown in the list and the detail form, in display order. The hosted payment
    /// gateway is deliberately absent: it appears through the plugin registry, not as a built-in.
    /// </summary>
    private static readonly Dictionary<string, IntegrationCatalogueEntry> BuiltIn = new()
    {
        [IntegrationSlugs.BankTransfer] = new("integrationMeta.bankTransfer.name", PaymentGateways,
            [
                new(BankTransferFields.AccountName, "integrationMeta.bankTransfer.fieldAccountName", Text, Required: false),
                new(BankTransferFields.Iban, "integrationMeta.bankTransfer.fieldIban", Text, Required: false),
                new(BankTransferFields.Swift, "integrationMeta.bankTransfer.fieldSwift", Text, Required: false),
                new(BankTransferFields.BankName, "reports.directDebit.bankName", Text, Required: false),
                new(BankTransferFields.Instructions, "integrationMeta.bankTransfer.fieldInstructions", TextArea, Required: false),
                new(BankTransferFields.ExpiryDays, "integrationMeta.bankTransfer.fieldExpiryDays", Text, Required: false),
            ],
            []),
        [IntegrationSlugs.Cpanel] = new("integrationMeta.cpanel.name", HostingProvisioning,
            [
                new("host", "integrationMeta.cpanel.fieldHost", Text, Required: true),
                new("port", PortLabel, Text, Required: false),
                new("username", UsernameLabel, Text, Required: true),
                new("api_token", "integrationMeta.cpanel.fieldApiToken", Password, Required: true),
            ],
            ["host", "username", "api_token"]),
        [IntegrationSlugs.Smtp] = new("integrationMeta.smtp.name", "Email / SMTP",
            [
                new("host", "integrationMeta.smtp.fieldHost", Text, Required: true),
                new("port", PortLabel, Text, Required: false),
                new("username", UsernameLabel, Text, Required: false),
                new("password", PasswordLabel, Password, Required: false),
                new("from_address", "integrationMeta.smtp.fieldFromAddress", Text, Required: true),
                new("encryption", "integrationMeta.smtp.fieldEncryption", Select, Required: false, Options: ["TLS", "SSL", "None"],
                    OptionLabels: new Dictionary<string, string> { ["None"] = "integrationMeta.smtp.encryptionNone" }),
            ],
            ["host", "username", "password", "from_address"]),
        [IntegrationSlugs.MaxMind] = new("integrationMeta.maxmind.name", "Fraud Protection",
            [
                new("account_id", "integrationMeta.maxmind.fieldAccountId", Text, Required: true),
                new("license_key", "integrationMeta.maxmind.fieldLicenseKey", Password, Required: true),
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
