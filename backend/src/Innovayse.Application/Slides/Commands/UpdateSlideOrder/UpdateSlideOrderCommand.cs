namespace Innovayse.Application.Slides.Commands.UpdateSlideOrder;

/// <summary>Represents a single slide-to-sort-order mapping.</summary>
/// <param name="Id">The slide identifier.</param>
/// <param name="SortOrder">The new sort order value for the slide.</param>
public record SlideOrderItem(int Id, int SortOrder);

/// <summary>Command to update the display order of multiple slides in a single operation.</summary>
/// <param name="Items">The list of slide ID and new sort order pairs.</param>
public record UpdateSlideOrderCommand(List<SlideOrderItem> Items);
