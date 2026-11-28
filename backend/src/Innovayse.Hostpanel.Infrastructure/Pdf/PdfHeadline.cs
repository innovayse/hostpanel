namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>The large line above the items table, optionally with a link under it.</summary>
/// <param name="Text">The headline, such as the amount due and its date.</param>
/// <param name="LinkText">The text of the link under it; null when there is no link.</param>
/// <param name="LinkUrl">Where the link goes; null when there is no link.</param>
internal sealed record PdfHeadline(string Text, string? LinkText, string? LinkUrl);
