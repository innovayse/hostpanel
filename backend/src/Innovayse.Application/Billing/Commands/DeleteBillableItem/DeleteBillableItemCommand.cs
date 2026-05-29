namespace Innovayse.Application.Billing.Commands.DeleteBillableItem;

/// <summary>Command to delete a billable item.</summary>
/// <param name="Id">The billable item ID to delete.</param>
public sealed record DeleteBillableItemCommand(int Id);
