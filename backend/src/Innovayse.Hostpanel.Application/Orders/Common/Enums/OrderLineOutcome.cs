namespace Innovayse.Hostpanel.Application.Orders.Common.Enums;

/// <summary>How an attempt to fulfil one order line ended.</summary>
public enum OrderLineOutcome
{
    /// <summary>The line was fulfilled: the service was created or the domain action done.</summary>
    Fulfilled = 0,

    /// <summary>The line failed, with a code the use case that refused it gave, or none.</summary>
    Failed = 1,
}
