namespace Innovayse.Application.Support.Commands.CreatePredefinedReply;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="CreatePredefinedReplyCommand"/> by creating a new predefined reply.</summary>
public sealed class CreatePredefinedReplyHandler(IPredefinedReplyRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new predefined reply and persists it.
    /// </summary>
    /// <param name="cmd">The create predefined reply command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created reply.</returns>
    public async Task<int> HandleAsync(CreatePredefinedReplyCommand cmd, CancellationToken ct)
    {
        var reply = PredefinedReply.Create(cmd.Name, cmd.Content, cmd.CategoryId);

        repo.Add(reply);
        await uow.SaveChangesAsync(ct);

        return reply.Id;
    }
}
