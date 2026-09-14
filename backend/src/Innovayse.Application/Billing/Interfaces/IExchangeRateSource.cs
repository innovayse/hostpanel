namespace Innovayse.Application.Billing.Interfaces;

/// <summary>Where exchange rates come from when the operator asks for them to be refreshed.</summary>
/// <remarks>
/// The contract is in the panel's own terms — "one unit of a currency is worth this much of the
/// base" — so a handler never learns which bank published the figures or which currency that
/// bank pivots on. Implementations live in Infrastructure.
/// </remarks>
public interface IExchangeRateSource
{
    /// <summary>Quotes each requested currency against the base.</summary>
    /// <param name="baseCode">ISO 4217 alpha code of the base currency, any case.</param>
    /// <param name="codes">ISO 4217 alpha codes to quote, any case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// "1 unit of <c>code</c> = X <paramref name="baseCode"/>", keyed by upper-case code. A code the
    /// source does not quote is absent rather than guessed.
    /// </returns>
    /// <exception cref="InvalidOperationException">The source cannot express anything against <paramref name="baseCode"/>.</exception>
    /// <exception cref="HttpRequestException">The source did not answer, or answered with an error.</exception>
    Task<IReadOnlyDictionary<string, decimal>> RatesToAsync(string baseCode, IEnumerable<string> codes, CancellationToken ct);
}
