namespace Innovayse.Application.Billing.Commands.DeleteClientTransaction;

/// <summary>Command to delete a client transaction and reverse any credit adjustments.</summary>
/// <param name="Id">The transaction's primary key.</param>
public record DeleteClientTransactionCommand(int Id);
