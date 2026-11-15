using System.Xml;
using System.Xml.Linq;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Currency;

/// <summary>Exchange rates from the European Central Bank's daily euro foreign exchange reference rates.</summary>
/// <remarks>
/// <para>
/// <b>Endpoint and shape.</b> <c>GET https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml</c> answers
/// an XML document in the <c>eurofxref</c> namespace: an envelope holding one <c>Cube</c> that holds a dated
/// <c>Cube</c> (attribute <c>time</c>, such as 2026-11-13), which holds one <c>Cube</c> per currency with the
/// attributes <c>currency</c> (USD) and <c>rate</c> (1.0845). The rate is how many units of the currency one
/// euro buys, so the euro itself is the unit everything is quoted in and is not listed.
/// </para>
/// <para>
/// <b>Arithmetic.</b> One unit of a currency is worth <c>1 / rate</c> euro, and <see cref="PivotRates"/> turns
/// that into the base the panel asks for (the euro, or any base the bank lists). A base the bank does not quote,
/// the dram for example, cannot anchor the conversion and the call refuses with
/// <c>EXCHANGE_RATE_BASE_NOT_QUOTED</c>.
/// </para>
/// <para>
/// <b>Working days only.</b> The bank publishes once on each TARGET working day, around 16:00 Central European
/// time. At any other time the same address answers with the last published set, so a weekend refresh keeps
/// the rates of the last working day; the date of the set is logged on every refresh.
/// </para>
/// <para>
/// <b>Failures.</b> A bank that does not answer, answers with an error status or with a body that is not the
/// published document is reported as <c>EXCHANGE_RATE_SOURCE_UNAVAILABLE</c>, logged here with the cause; the
/// bank's own text never reaches the response and the stored rates stay as they were.
/// </para>
/// <para>
/// Registered as a typed client with the <c>Ecb</c> read-only resilience profile. The document is read with
/// DTD processing prohibited, so a body the bank did not publish cannot pull in an entity or an outside file.
/// </para>
/// </remarks>
public sealed class EcbExchangeRateSource : IExchangeRateSource
{
    /// <summary>The bank's origin; the typed client registration sets it as the base address.</summary>
    public const string BaseAddress = "https://www.ecb.europa.eu/";

    /// <summary>The daily reference rates, relative to <see cref="BaseAddress"/>.</summary>
    public const string DailyRatesPath = "stats/eurofxref/eurofxref-daily.xml";

    /// <summary>The currency every quote is expressed in, and the only one the document omits.</summary>
    private const string PivotCode = "EUR";

    /// <summary>The XML namespace of the reference rates document.</summary>
    private static readonly XNamespace Eurofxref = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";

    /// <summary>The typed client, with the bank's origin as its base address.</summary>
    private readonly HttpClient _http;

    /// <summary>Records why a refresh failed and which day's set it read; the response carries only the fixed code.</summary>
    private readonly ILogger<EcbExchangeRateSource> _logger;

    /// <summary>Initializes a new instance of the <see cref="EcbExchangeRateSource"/> class.</summary>
    /// <param name="http">The typed client, with the bank's origin as its base address.</param>
    /// <param name="logger">Records why a refresh failed and which day's set it read; the response carries only the fixed code.</param>
    public EcbExchangeRateSource(HttpClient http, ILogger<EcbExchangeRateSource> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var quotes = await FetchEuroPerUnitAsync(ct);
        if (quotes is null)
        {
            return Result<ExchangeRateQuotes>.FailureCode(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
        }

        return PivotRates.Quote(lookup, PivotCode, quotes);
    }

    /// <summary>Downloads the reference rates and turns each into "euro per one unit".</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The euro value of one unit, keyed by upper-case code, a currency without a usable rate dropped; or
    /// <c>null</c> when the bank did not answer with the published document, the cause logged.
    /// </returns>
    private async Task<Dictionary<string, decimal>?> FetchEuroPerUnitAsync(CancellationToken ct)
    {
        var body = await ExchangeRateFeed.TryReadAsync(_http, DailyRatesPath, _logger, ct);
        if (body is null)
        {
            return null;
        }

        XDocument document;
        try
        {
            using var text = new StringReader(body);
            using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "The exchange rate document was not the published XML.");
            return null;
        }

        var dated = document.Root?.Element(Eurofxref + "Cube")?.Element(Eurofxref + "Cube");
        if (dated is null)
        {
            _logger.LogWarning("The exchange rate document held no dated set of rates.");
            return null;
        }

        _logger.LogInformation("Read the European Central Bank reference rates published for {Date}.", dated.Attribute("time")?.Value);

        var quotes = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var cube in dated.Elements(Eurofxref + "Cube"))
        {
            var code = cube.Attribute("currency")?.Value;
            var perEuro = PivotRates.ParsePositive(cube.Attribute("rate")?.Value);
            if (!string.IsNullOrEmpty(code) && perEuro is not null)
            {
                quotes[code.ToUpperInvariant()] = 1m / perEuro.Value;
            }
        }

        return quotes;
    }
}
