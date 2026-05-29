namespace Innovayse.Application.Billing.Commands.DeleteBillableItem;

<<<<<<< HEAD
/// <summary>Command to delete a billable item.</summary>
/// <param name="Id">The billable item ID to delete.</param>
public sealed record DeleteBillableItemCommand(int Id);
=======
/// <summary>Command to delete an uninvoiced billable item.</summary>
/// <param name="Id">The billable item primary key.</param>
public record DeleteBillableItemCommand(int Id);
>>>>>>> origin/main
