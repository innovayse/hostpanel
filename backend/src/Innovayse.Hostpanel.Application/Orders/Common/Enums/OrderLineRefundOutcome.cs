namespace Innovayse.Hostpanel.Application.Orders.Common.Enums;

/// <summary>How far a refund of a failed order line got when it ended without giving back the whole cost.</summary>
public enum OrderLineRefundOutcome
{
    /// <summary>Nothing was given back, so the line is failed again and may be retried.</summary>
    NothingRefunded = 0,

    /// <summary>Some of the cost was given back and some was not, so staff must finish the refund by hand.</summary>
    PartlyRefunded = 1,
}
