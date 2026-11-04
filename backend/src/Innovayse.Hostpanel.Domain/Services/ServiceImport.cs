using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// A service as another billing system recorded it, carried into <see cref="ClientService.Import"/>
/// so it lands in the state it already has rather than walking the lifecycle again.
/// </summary>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ProductId">FK to the product the service is of.</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="Status">The status the service already has.</param>
/// <param name="RegisteredAt">When the service was ordered (UTC); becomes the creation date and anchors the billing period.</param>
/// <param name="StatusChangedAt">When the service was suspended or terminated (UTC); ignored for any other status.</param>
/// <param name="SuspensionSource">Why a suspended service is suspended; ignored for any other status.</param>
/// <param name="HostingAccount">The server, domain, panel reference and credentials the service is hosted under.</param>
/// <param name="ExternalId">The service's own id in the source system, kept so a retried import recognises it; <see langword="null"/> when the source sent none.</param>
public sealed record ServiceImport(
    int ClientId,
    int ProductId,
    BillingCycle BillingCycle,
    ServiceStatus Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset StatusChangedAt,
    SuspensionSource SuspensionSource,
    ServiceHostingAccount HostingAccount,
    string? ExternalId = null);
