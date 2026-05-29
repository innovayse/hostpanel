namespace Innovayse.Application.Support.Commands.UpdatePredefinedReply;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="UpdatePredefinedReplyCommand"/> by updating the predefined reply fields.</summary>
public sealed class UpdatePredefinedReplyHandler(IPredefinedReplyRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates an existing predefined reply.
    /// </summary>
    /// <param name="cmd">The update predefined reply command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the reply is not found.</exception>
    public async Task HandleAsync(UpdatePredefinedReplyCommand cmd, CancellationToken ct)
    {
        var reply = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Predefined reply {cmd.Id} not found.");

        reply.Update(cmd.Name, cmd.Content, cmd.CategoryId);
        await uow.SaveChangesAsync(ct);
    }
}
