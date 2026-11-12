using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;

/// <summary>
/// Asks the minFraud Insights service how risky a request looks, when the operator has set the integration up and switched it on
/// (<c>integration:maxmind:*</c> on the Integrations page). Registered as a typed <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// Only the caller's IP address, the domain of the e-mail address and the billing country leave the host; the full address is
/// not sent. Every way the call can go wrong (not set up, unreachable, refused, slow, unreadable) ends as <see langword="null"/>
/// with a warning that carries no response body: the local score decides alone, and the order is never failed by this service.
/// </remarks>
public sealed class MaxMindFraudScoringProvider : IFraudScoringProvider
{
    /// <summary>The service's address; the client is registered with it.</summary>
    public const string BaseAddress = "https://minfraud.maxmind.com/";

    /// <summary>The Insights endpoint, relative to <see cref="BaseAddress"/>.</summary>
    private const string InsightsPath = "minfraud/v2.0/insights";

    /// <summary>The service's score from which the reason <see cref="FraudReasonCode.ProviderHighRisk"/> is reported.</summary>
    private const int HighRiskScore = 50;

    /// <summary>The service names its members in snake case; absent members stay out of the request.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The typed client, already pointed at <see cref="BaseAddress"/>.</summary>
    private readonly HttpClient _http;

    /// <summary>The data context, for the integration's stored settings.</summary>
    private readonly AppDbContext _db;

    /// <summary>Reveals the stored license key.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Logs why the service could not be used.</summary>
    private readonly ILogger<MaxMindFraudScoringProvider> _logger;

    /// <summary>Initializes the provider.</summary>
    /// <param name="http">The typed client, already pointed at <see cref="BaseAddress"/>.</param>
    /// <param name="db">The data context, for the integration's stored settings.</param>
    /// <param name="cipher">Reveals the stored license key.</param>
    /// <param name="logger">Logs why the service could not be used.</param>
    public MaxMindFraudScoringProvider(
        HttpClient http, AppDbContext db, ISecretSettingCipher cipher, ILogger<MaxMindFraudScoringProvider> logger)
    {
        _http = http;
        _db = db;
        _cipher = cipher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FraudProviderAssessmentDto?> ScoreAsync(FraudProviderContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var credentials = await ReadCredentialsAsync(ct);
        if (credentials is null)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, InsightsPath)
            {
                Content = JsonContent.Create(BuildRequest(context), options: JsonOptions),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Value.AccountId}:{credentials.Value.LicenseKey}")));

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // The body can echo the request; only the status is logged.
                _logger.LogWarning("The fraud scoring service answered {StatusCode}; scoring locally.", (int)response.StatusCode);
                return null;
            }

            var wire = await response.Content.ReadFromJsonAsync<MaxMindInsightsWire>(JsonOptions, ct);
            return wire is null ? null : Map(wire);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
            || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Type only: the message of a failed request can carry the address that was asked about.
            _logger.LogWarning("The fraud scoring service could not be used ({ExceptionType}); scoring locally.", ex.GetType().Name);
            return null;
        }
    }

    /// <summary>Builds the Insights request body from what the host asks about.</summary>
    /// <param name="context">What the service is asked.</param>
    /// <returns>An object the serializer writes as the service's request.</returns>
    private static object BuildRequest(FraudProviderContext context)
    {
        var at = context.Email?.LastIndexOf('@') ?? -1;
        var domain = at >= 0 && at < context.Email!.Length - 1 ? context.Email[(at + 1)..] : null;

        return new
        {
            Device = new { context.IpAddress },
            Email = domain is null ? null : new { Domain = domain },
            Billing = string.IsNullOrWhiteSpace(context.BillingCountry) ? null : new { Country = context.BillingCountry },
        };
    }

    /// <summary>Turns the service's answer into the host's assessment.</summary>
    /// <param name="wire">The service's answer.</param>
    /// <returns>The assessment; an answer without a score reads as no risk seen.</returns>
    private static FraudProviderAssessmentDto Map(MaxMindInsightsWire wire)
    {
        var score = (int)Math.Clamp(Math.Round(wire.RiskScore ?? 0, MidpointRounding.AwayFromZero), 0, 100);
        var traits = wire.IpAddress?.Traits;
        var anonymous = traits is not null
            && (traits.IsAnonymous == true || traits.IsAnonymousVpn == true || traits.IsHostingProvider == true
                || traits.IsPublicProxy == true || traits.IsResidentialProxy == true || traits.IsTorExitNode == true);

        var reasons = new List<FraudReasonCode>();
        if (score >= HighRiskScore)
        {
            reasons.Add(FraudReasonCode.ProviderHighRisk);
        }

        if (anonymous)
        {
            reasons.Add(FraudReasonCode.AnonymousIp);
        }

        var country = wire.IpAddress?.Country?.IsoCode;
        return new FraudProviderAssessmentDto(score, reasons, string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant());
    }

    /// <summary>Reads the stored account id and license key, when the integration is set up and switched on.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The credentials, or <see langword="null"/> when the integration is off or incomplete.</returns>
    private async Task<(string AccountId, string LicenseKey)?> ReadCredentialsAsync(CancellationToken ct)
    {
        var enabledKey = IntegrationSettingKeys.EnabledKey(IntegrationSlugs.MaxMind);
        var accountKey = IntegrationSettingKeys.FieldKey(IntegrationSlugs.MaxMind, MaxMindFields.AccountId);
        var licenseKey = IntegrationSettingKeys.FieldKey(IntegrationSlugs.MaxMind, MaxMindFields.LicenseKey);
        string[] keys = [enabledKey, accountKey, licenseKey];

        var stored = await _db.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        if (!stored.TryGetValue(enabledKey, out var enabled)
            || !string.Equals(enabled, IntegrationSettingKeys.EnabledValue, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The license key is a secret setting, stored encrypted; the account id is plain.
        var accountId = stored.GetValueOrDefault(accountKey)?.Trim();
        var license = stored.TryGetValue(licenseKey, out var protectedKey) && !string.IsNullOrWhiteSpace(protectedKey)
            ? _cipher.Reveal(protectedKey).Trim()
            : null;

        return string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(license) ? null : (accountId, license);
    }
}
