using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>
/// One decision about a registration or an order: what was checked, from where, what it scored and what came of it. The log is
/// append only, and it is also the memory the velocity and shared IP signals count from.
/// </summary>
public sealed class FraudLogEntry : BaseEntity, IAggregateRoot
{
    /// <summary>Longest IP address text the column holds (an IPv6 address with a scope).</summary>
    public const int IpAddressMaxLength = 45;

    /// <summary>Longest e-mail address the column holds.</summary>
    public const int EmailMaxLength = 320;

    /// <summary>Gets what was checked.</summary>
    public FraudCheckKind Kind { get; private set; }

    /// <summary>Gets what was decided.</summary>
    public FraudDecision Decision { get; private set; }

    /// <summary>Gets the client the request was for, or <see langword="null"/> before one existed or when it was refused.</summary>
    public int? ClientId { get; private set; }

    /// <summary>Gets the order the decision is about, or <see langword="null"/> for a registration or an order that was refused.</summary>
    public int? OrderId { get; private set; }

    /// <summary>Gets the caller's IP address, or <see langword="null"/> when it was not known.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the e-mail address checked, in lower case, or <see langword="null"/> when none was known.</summary>
    public string? Email { get; private set; }

    /// <summary>Gets the risk score, 0 to 100.</summary>
    public int Score { get; private set; }

    /// <summary>Gets the reasons as stored, see <see cref="FraudReasonText"/>.</summary>
    public string ReasonCodes { get; private set; } = string.Empty;

    /// <summary>Gets when the decision was made (UTC); stamped on save.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the reasons the score came from.</summary>
    public IReadOnlyList<FraudReasonCode> Reasons => FraudReasonText.Parse(ReasonCodes);

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private FraudLogEntry() { }

    /// <summary>Records a decision.</summary>
    /// <param name="kind">What was checked.</param>
    /// <param name="decision">What was decided.</param>
    /// <param name="clientId">The client, when there is one.</param>
    /// <param name="orderId">The order, when there is one.</param>
    /// <param name="ipAddress">The caller's IP address, when known.</param>
    /// <param name="email">The e-mail address checked, when known.</param>
    /// <param name="score">The risk score, 0 to 100.</param>
    /// <param name="reasons">The reasons the score came from.</param>
    /// <returns>A new log entry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The score is outside 0 to 100.</exception>
    public static FraudLogEntry Create(
        FraudCheckKind kind,
        FraudDecision decision,
        int? clientId,
        int? orderId,
        string? ipAddress,
        string? email,
        int score,
        IReadOnlyList<FraudReasonCode> reasons)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(score, FraudAssessment.MaxScore);

        return new FraudLogEntry
        {
            Kind = kind,
            Decision = decision,
            ClientId = clientId,
            OrderId = orderId,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            Score = score,
            ReasonCodes = FraudReasonText.Format(reasons),
        };
    }
}
