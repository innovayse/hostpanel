namespace Innovayse.Application.Services.Commands.DeleteCancellationRequest;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Deletes a cancellation request by primary key.</summary>
public sealed class DeleteCancellationRequestHandler(
    ICancellationRequestRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteCancellationRequestCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the cancellation request is not found.</exception>
    public async Task HandleAsync(DeleteCancellationRequestCommand cmd, CancellationToken ct)
    {
        var request = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Cancellation request {cmd.Id} not found.");

        repo.Remove(request);
        await uow.SaveChangesAsync(ct);
    }
}
