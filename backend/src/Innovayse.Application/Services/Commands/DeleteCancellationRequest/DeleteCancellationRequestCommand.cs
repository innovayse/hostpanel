namespace Innovayse.Application.Services.Commands.DeleteCancellationRequest;

/// <summary>Command to delete a cancellation request.</summary>
/// <param name="Id">The cancellation request primary key.</param>
public record DeleteCancellationRequestCommand(int Id);
