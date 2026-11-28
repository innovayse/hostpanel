using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The one catalogue of built-in integrations: display name, category, form fields and the fields
/// that make an integration "configured". The list, detail and connection-test use cases all read
/// it, so an integration is added or changed here and nowhere else. The catalogue carries no wording: the admin owns the
/// name, field labels and option labels of the built-in integrations and resolves them from the slug and the field
/// key. The category is a fixed wire value the admin matches against its own closed set of category names and never
/// shows as written, so a value is never reworded.
/// </summary>
public static class IntegrationCatalogue
{
    /// <summary>Category wire value of the payment gateways.</summary>
    private const string PaymentGateways = "Payment Gateways";

    /// <summary>Category wire value of the hosting and provisioning panels.</summary>
    private const string HostingProvisioning = "Hosting / Provisioning";

    /// <summary>Category wire value of the email delivery integrations.</summary>
    private const string EmailSmtp = "Email / SMTP";

    /// <summary>Category wire value of the fraud screening integrations.</summary>
    private const string FraudProtection = "Fraud Protection";

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
        [IntegrationSlugs.BankTransfer] = new(PaymentGateways,
            [
                new(BankTransferFields.AccountName, Text, Required: false),
                new(BankTransferFields.Iban, Text, Required: false),
                new(BankTransferFields.Swift, Text, Required: false),
                new(BankTransferFields.BankName, Text, Required: false),
                new(BankTransferFields.Instructions, TextArea, Required: false),
                new(BankTransferFields.ExpiryDays, Text, Required: false),
            ],
            []),
        [IntegrationSlugs.Cpanel] = new(HostingProvisioning,
            [
                new("host", Text, Required: true),
                new("port", Text, Required: false),
                new("username", Text, Required: true),
                new("api_token", Password, Required: true),
            ],
            ["host", "username", "api_token"]),
        [IntegrationSlugs.Smtp] = new(EmailSmtp,
            [
                new("host", Text, Required: true),
                new("port", Text, Required: false),
                new("username", Text, Required: false),
                new("password", Password, Required: false),
                new("from_address", Text, Required: true),
                new("encryption", Select, Required: false, Options: ["TLS", "SSL", "None"]),
            ],
            ["host", "username", "password", "from_address"]),
        [IntegrationSlugs.MaxMind] = new(FraudProtection,
            [
                new("account_id", Text, Required: true),
                new("license_key", Password, Required: true),
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
