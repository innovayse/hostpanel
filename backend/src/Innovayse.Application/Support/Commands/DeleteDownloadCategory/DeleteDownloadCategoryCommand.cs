namespace Innovayse.Application.Support.Commands.DeleteDownloadCategory;

/// <summary>Command to delete a download category by ID.</summary>
/// <param name="Id">The category identifier to delete.</param>
public record DeleteDownloadCategoryCommand(int Id);
