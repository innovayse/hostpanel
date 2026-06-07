namespace Innovayse.Application.Slides.Commands.DeleteSlide;

using Innovayse.Application.Common;
using Innovayse.Domain.Slides.Interfaces;

/// <summary>
/// Handles <see cref="DeleteSlideCommand"/> by loading the slide, removing it from
/// the repository, and persisting the deletion.
/// </summary>
/// <param name="slideRepo">Slide repository for loading and removal.</param>
/// <param name="uow">Unit of work for committing the deletion.</param>
public sealed class DeleteSlideHandler(ISlideRepository slideRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes a slide permanently from the persistence store.
    /// </summary>
    /// <param name="cmd">The delete command containing the slide ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the slide with the given ID is not found.</exception>
    public async Task HandleAsync(DeleteSlideCommand cmd, CancellationToken ct)
    {
        var slide = await slideRepo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Slide {cmd.Id} not found.");

        slideRepo.Remove(slide);
        await uow.SaveChangesAsync(ct);
    }
}
