using PdfSharp.Drawing;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>The small coloured status label drawn beside the title.</summary>
/// <param name="Text">The status words, in the language of the document.</param>
/// <param name="Ink">The colour of the text.</param>
/// <param name="Fill">The colour of the rounded background.</param>
internal sealed record PdfPill(string Text, XColor Ink, XColor Fill);
