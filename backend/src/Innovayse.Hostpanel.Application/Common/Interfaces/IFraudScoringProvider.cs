using Innovayse.Hostpanel.Application.Fraud.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// An external service that rates how risky a registration or order looks. It adds to the local signals and is never required:
/// without it, or when it fails, the local score decides.
/// </summary>
public interface IFraudScoringProvider
{
    /// <summary>Asks the service about a request.</summary>
    /// <param name="context">What the service is asked.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The service's assessment, or <see langword="null"/> when the service is not set up or switched on, or could not be reached or
    /// understood; the adapter logs the cause, so a caller treats every <see langword="null"/> the same way and falls back to local scoring.
    /// </returns>
    public Task<FraudProviderAssessmentDto?> ScoreAsync(FraudProviderContext context, CancellationToken ct);
}
