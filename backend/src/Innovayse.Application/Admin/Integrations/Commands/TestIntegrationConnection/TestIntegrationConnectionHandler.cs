namespace Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="TestIntegrationConnectionCommand"/> by checking whether all
/// required fields for the integration contain non-empty stored values, and, for
/// integrations that support it, running a live probe against the provider.
/// </summary>
/// <param name="settings">Setting repository for key-value lookups.</param>
/// <param name="pluginResolver">Payment plugin resolver, used for live gateway probes.</param>
public sealed class TestIntegrationConnectionHandler(
    ISettingRepository settings,
    IPaymentPluginResolver pluginResolver)
{
    /// <summary>
    /// Plugin id / slug of the Inecobank gateway. The provider assembly already exposes this
    /// as <c>InecobankPaymentGateway.PluginId</c>, but Application cannot reference a provider
    /// assembly, so it is repeated here as a single source shared by both usages below.
    /// </summary>
    private const string InecobankSlug = "innovayse-inecobank";

    /// <summary>
    /// Static metadata for every integration.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Category, string[] RequiredFields, string[] AllFields)> _meta = new()
    {
        ["stripe"] = ("Stripe", "Payment Gateways", ["secret_key"], ["secret_key", "publishable_key", "webhook_secret", "mode"]),
        ["paypal"] = ("PayPal", "Payment Gateways", ["client_id", "client_secret"], ["client_id", "client_secret", "mode"]),
        ["bank-transfer"] = ("Bank Transfer", "Payment Gateways", [], ["account_name", "iban", "bank_name", "instructions"]),
        ["namecheap"] = ("Namecheap", "Domain Registrars", ["api_key", "api_username", "client_ip"], ["api_key", "api_username", "client_ip"]),
        ["nameam"] = ("Name.am", "Domain Registrars", ["email", "password"], ["email", "password", "api_url"]),
        ["resellerclub"] = ("ResellerClub", "Domain Registrars", ["reseller_id", "api_key"], ["reseller_id", "api_key"]),
        ["enom"] = ("ENOM", "Domain Registrars", ["account_id", "api_key"], ["account_id", "api_key"]),
        ["cpanel"] = ("cPanel WHM", "Hosting / Provisioning", ["host", "username", "api_token"], ["host", "port", "username", "api_token"]),
        ["plesk"] = ("Plesk", "Hosting / Provisioning", ["host", "username", "password"], ["host", "port", "username", "password"]),
        ["cwp"] = ("CentOS Web Panel", "Hosting / Provisioning", ["host", "api_key"], ["host", "port", "api_key"]),
        ["cwp7"] = ("CWP7", "Hosting / Provisioning", [], []),
        ["smtp"] = ("SMTP Server", "Email / SMTP", ["host", "username", "password", "from_address"], ["host", "port", "username", "password", "from_address", "encryption"]),
        ["maxmind"] = ("MaxMind", "Fraud Protection", ["account_id", "license_key"], ["account_id", "license_key"]),
        [InecobankSlug] = ("Inecobank", "Payment Gateways",
            ["gateway_url", "username", "password"],
            ["gateway_url", "username", "password", "currency", "language"]),
    };

    /// <summary>
    /// Checks whether all required credential fields for the given integration slug are
    /// stored and non-empty, then returns a result describing the outcome.
    /// </summary>
    /// <param name="command">Command containing the integration slug to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IntegrationTestResultDto"/> with Success = true when fully
    /// configured, or Success = false listing the missing fields.
    /// Returns a not-found failure result when the slug is unrecognised.
    /// </returns>
    public async Task<IntegrationTestResultDto> HandleAsync(
        TestIntegrationConnectionCommand command, CancellationToken ct)
    {
        var testedAt = DateTimeOffset.UtcNow;

        if (!_meta.TryGetValue(command.Slug, out var meta))
        {
            return new IntegrationTestResultDto(
                Success: false,
                Message: $"Unknown integration slug '{command.Slug}'.",
                TestedAt: testedAt);
        }

        // Integrations with no required fields (e.g. bank-transfer) are always "configured".
        if (meta.RequiredFields.Length == 0)
        {
            return new IntegrationTestResultDto(
                Success: true,
                Message: "Connection validated -- all required fields are configured.",
                TestedAt: testedAt);
        }

        var all = await settings.ListAsync(ct);
        var prefix = IntegrationSettingKeys.Prefix(command.Slug);
        var lookup = all
            .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var missing = meta.RequiredFields
            .Where(field =>
                !lookup.TryGetValue(IntegrationSettingKeys.FieldKey(command.Slug, field), out var val)
                || string.IsNullOrWhiteSpace(val))
            .ToList();

        if (missing.Count == 0)
        {
            // Live probe for hosted-gateway plugins: a status query with a bogus order id
            // distinguishes valid credentials ("unregistered orderId" → Declined result)
            // from rejected ones (access-denied error → exception).
            if (command.Slug == InecobankSlug)
            {
                try
                {
                    var plugin = await pluginResolver.ResolveAsync(command.Slug, ct);
                    if (plugin is null)
                    {
                        return new IntegrationTestResultDto(
                            Success: false,
                            Message: "Integration is disabled or the plugin is not loaded.",
                            TestedAt: testedAt);
                    }

                    await plugin.GetStatusAsync("connection-test-probe", ct);
                    return new IntegrationTestResultDto(
                        Success: true,
                        Message: "Gateway reachable and credentials accepted.",
                        TestedAt: testedAt);
                }
                catch (OperationCanceledException)
                {
                    // An admin-cancelled request (e.g. navigating away mid-probe) is not a
                    // gateway failure — let it propagate rather than reporting "test failed".
                    throw;
                }
                catch (Exception ex)
                {
                    return new IntegrationTestResultDto(
                        Success: false,
                        Message: $"Gateway test failed: {ex.Message}",
                        TestedAt: testedAt);
                }
            }

            return new IntegrationTestResultDto(
                Success: true,
                Message: "Connection validated -- all required fields are configured.",
                TestedAt: testedAt);
        }

        var missingList = string.Join(", ", missing);
        return new IntegrationTestResultDto(
            Success: false,
            Message: $"Missing required fields: {missingList}.",
            TestedAt: testedAt);
    }
}
