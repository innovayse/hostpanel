namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A Pending order whose first line buys a digital product and whose second line buys hosting.</summary>
/// <param name="OrderId">The order's id.</param>
/// <param name="ClientId">The ordering client.</param>
/// <param name="DigitalProductId">The digital product of the first line.</param>
/// <param name="HostingProductId">The hosting product of the second line.</param>
public sealed record DigitalAndHostingOrder(int OrderId, int ClientId, int DigitalProductId, int HostingProductId);
