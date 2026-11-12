using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>The external scoring service, which a test scripts: it answers a fixed assessment, or nothing, and remembers what it was asked.</summary>
/// <param name="answer">What the service answers; <see langword="null"/> for a service that is not set up or could not be reached.</param>
public sealed class FakeFraudScoringProvider(FraudProviderAssessmentDto? answer) : IFraudScoringProvider
{
    /// <summary>Gets what the service was asked, in order.</summary>
    public List<FraudProviderContext> Asked { get; } = [];

    /// <inheritdoc/>
    public Task<FraudProviderAssessmentDto?> ScoreAsync(FraudProviderContext context, CancellationToken ct)
    {
        Asked.Add(context);
        return Task.FromResult(answer);
    }
}
