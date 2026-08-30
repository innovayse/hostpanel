namespace Innovayse.Application.Billing.Queries.GetMyServiceInvoices;

using Innovayse.Application.Services.Common;

/// <summary>Returns the invoices charged to one of the calling client's own services.</summary>
/// <remarks>
/// Carries a service id but no client id. Which account the service must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can, and the repository read is scoped by that client as well as by
/// the service.
/// </remarks>
/// <param name="ServiceId">Primary key of the service, which must belong to the caller.</param>
public sealed record GetMyServiceInvoicesQuery(int ServiceId) : ICallerScopedServiceMessage;
