namespace Innovayse.Application.Billing.Commands.DeleteBillableItem;

/// <summary>Command to delete an uninvoiced billable item.</summary>
/// <param name="Id">The billable item primary key.</param>
public record DeleteBillableItemCommand(int Id);
