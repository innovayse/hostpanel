namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>An active service seeded for a plan change test, with the plan it runs on and the plan it can move to.</summary>
/// <param name="ClientId">The owning client's id.</param>
/// <param name="ServiceId">The service's id.</param>
/// <param name="FromProductId">The product the service runs on.</param>
/// <param name="ToProductId">The other product of the same group the service can move to.</param>
/// <param name="FromPrice">The monthly USD price of the product the service runs on, which is also the service's recurring amount.</param>
/// <param name="ToPrice">The monthly USD price of the other product.</param>
/// <param name="NextRenewalAt">The renewal date that ends the current billing cycle.</param>
public sealed record SeededPlanChange(
    int ClientId,
    int ServiceId,
    int FromProductId,
    int ToProductId,
    decimal FromPrice,
    decimal ToPrice,
    DateTimeOffset NextRenewalAt);
