namespace Innovayse.Hostpanel.Domain.Fraud.Enums;

/// <summary>What was decided about a registration or an order, automatically or by staff.</summary>
public enum FraudDecision
{
    /// <summary>The score was below the review threshold; the request went through.</summary>
    Approved = 1,

    /// <summary>The score reached the review threshold; the order waits for staff.</summary>
    Review = 2,

    /// <summary>The score reached the reject threshold, or a block list matched; the request was refused.</summary>
    Rejected = 3,

    /// <summary>Staff approved an order that was held for review.</summary>
    ManualApproved = 4,

    /// <summary>Staff rejected an order that was held for review.</summary>
    ManualRejected = 5,
}
