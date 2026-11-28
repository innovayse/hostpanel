namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>A paragraph under the totals: a heading in bold and the text below it.</summary>
/// <param name="Heading">The bold line above the text.</param>
/// <param name="Text">The paragraph.</param>
internal sealed record PdfNote(string Heading, string Text);
