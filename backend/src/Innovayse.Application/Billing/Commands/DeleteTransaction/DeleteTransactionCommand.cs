namespace Innovayse.Application.Billing.Commands.DeleteTransaction;

/// <summary>Command to delete a transaction and reverse any credit adjustments.</summary>
/// <param name="Id">The transaction's primary key.</param>
public record DeleteTransactionCommand(int Id);
