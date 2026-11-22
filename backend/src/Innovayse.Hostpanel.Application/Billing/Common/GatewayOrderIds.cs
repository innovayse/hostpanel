namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The shape of a gateway-side order id as the host accepts it from a plugin: what <c>Invoices.GatewayOrderId</c> can hold,
/// and nothing a header, a log line or a partition key could be confused by. Checked in one place, by the plugin's request
/// to read an order and by the use case that serves it.
/// </summary>
public static class GatewayOrderIds
{
    /// <summary>The longest gateway order id the invoice stores, in characters.</summary>
    public const int MaxLength = 255;

    /// <summary>Tells whether a text is a well-formed gateway order id.</summary>
    /// <param name="gatewayOrderId">The id as the plugin sent it.</param>
    /// <returns>
    /// <see langword="true"/> when it is 1 to <see cref="MaxLength"/> characters with no control character and no white space
    /// at either end.
    /// </returns>
    public static bool IsWellFormed(string? gatewayOrderId)
    {
        return !string.IsNullOrEmpty(gatewayOrderId)
            && gatewayOrderId.Length <= MaxLength
            && gatewayOrderId.Trim() == gatewayOrderId
            && !gatewayOrderId.Any(char.IsControl);
    }
}
