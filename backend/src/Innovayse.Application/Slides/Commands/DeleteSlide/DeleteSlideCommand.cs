namespace Innovayse.Application.Slides.Commands.DeleteSlide;

/// <summary>Command to permanently delete a homepage slide by ID.</summary>
/// <param name="Id">The identifier of the slide to delete.</param>
public record DeleteSlideCommand(int Id);
