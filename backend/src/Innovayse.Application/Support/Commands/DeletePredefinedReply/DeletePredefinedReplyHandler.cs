namespace Innovayse.Application.Support.Commands.DeletePredefinedReply;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="DeletePredefinedReplyCommand"/> by permanently removing the reply.</summary>
public sealed class DeletePredefinedReplyHandler(IPredefinedReplyRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes a predefined reply.
    /// </summary>
    /// <param name="cmd">The delete predefined reply command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the reply is not found.</exception>
    public async Task HandleAsync(DeletePredefinedReplyCommand cmd, CancellationToken ct)
    {
        var reply = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Predefined reply {cmd.Id} not found.");

        repo.Remove(reply);
        await uow.SaveChangesAsync(ct);
    }
}
