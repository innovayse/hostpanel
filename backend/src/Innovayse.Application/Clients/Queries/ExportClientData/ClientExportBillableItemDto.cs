namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>
/// One billable item, as the <c>billableItems</c> section of a client data export lists it.
/// </summary>
/// <param name="Id">The billable item's primary key.</param>
/// <param name="Description">What is being charged for.</param>
/// <param name="Amount">Amount to be charged.</param>
/// <param name="NextDueDate">When it is next invoiced, or null for a one-off already dealt with.</param>
public sealed record ClientExportBillableItemDto(
    int Id,
    string Description,
    decimal Amount,
    DateTimeOffset? NextDueDate);
