namespace Innovayse.Application.Slides.Commands.UpdateSlideOrder;

/// <summary>Represents a single slide-to-sort-order mapping.</summary>
/// <param name="Id">The slide identifier.</param>
/// <param name="SortOrder">The new sort order value for the slide.</param>
public record SlideOrderItem(int Id, int SortOrder);
