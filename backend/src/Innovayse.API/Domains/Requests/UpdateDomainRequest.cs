namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for updating editable domain fields.</summary>
public sealed class UpdateDomainRequest
{
    /// <summary>One-time registration cost.</summary>
    public required decimal FirstPaymentAmount { get; init; }

    /// <summary>Recurring renewal amount.</summary>
    public required decimal RecurringAmount { get; init; }

    /// <summary>Payment method label.</summary>
    public string? PaymentMethod { get; init; }

    /// <summary>Applied promotion code.</summary>
    public string? PromotionCode { get; init; }

    /// <summary>External subscription reference.</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>Free-text admin notes.</summary>
    public string? AdminNotes { get; init; }

    /// <summary>Expiry date (ISO 8601).</summary>
    public required string ExpiresAt { get; init; }

    /// <summary>Next due date (ISO 8601).</summary>
    public required string NextDueDate { get; init; }

    /// <summary>Registration period in years.</summary>
    public required int RegistrationPeriod { get; init; }

    /// <summary>Domain lifecycle status.</summary>
    public required string Status { get; init; }

    /// <summary>Nameserver hostnames (up to 5).</summary>
    public List<string> Nameservers { get; init; } = [];
}
