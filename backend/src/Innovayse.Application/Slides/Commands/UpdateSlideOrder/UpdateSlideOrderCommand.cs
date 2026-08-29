namespace Innovayse.Application.Slides.Commands.UpdateSlideOrder;

/// <summary>Command to update the display order of multiple slides in a single operation.</summary>
/// <param name="Items">The list of slide ID and new sort order pairs.</param>
public record UpdateSlideOrderCommand(List<SlideOrderItem> Items);
