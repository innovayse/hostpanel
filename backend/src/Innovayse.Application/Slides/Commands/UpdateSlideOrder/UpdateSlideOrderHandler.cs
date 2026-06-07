namespace Innovayse.Application.Slides.Commands.UpdateSlideOrder;

using Innovayse.Application.Common;
using Innovayse.Domain.Slides.Interfaces;

/// <summary>
/// Handles <see cref="UpdateSlideOrderCommand"/> by loading all slides, applying the
/// new sort order values for matching IDs, and persisting the changes in one transaction.
/// </summary>
/// <param name="slideRepo">Slide repository for listing and loading all slides.</param>
/// <param name="uow">Unit of work for committing the reordering.</param>
public sealed class UpdateSlideOrderHandler(ISlideRepository slideRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates the sort order for each slide listed in the command.
    /// Slides whose IDs are not present in the command are left unchanged.
    /// </summary>
    /// <param name="cmd">The command containing slide ID and new sort order pairs.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(UpdateSlideOrderCommand cmd, CancellationToken ct)
    {
        var slides = await slideRepo.ListAllAsync(ct);
        var orderMap = cmd.Items.ToDictionary(i => i.Id, i => i.SortOrder);

        foreach (var slide in slides)
        {
            if (orderMap.TryGetValue(slide.Id, out var newOrder))
            {
                slide.Update(
                    slide.IconName,
                    slide.BrandColor,
                    slide.ImageUrl,
                    slide.DemoUrl,
                    slide.LearnMoreUrl,
                    slide.ProductId,
                    newOrder,
                    slide.IsActive,
                    slide.TargetAudience,
                    slide.VisibleFrom,
                    slide.VisibleUntil);
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
