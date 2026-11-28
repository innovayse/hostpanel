namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>A shaded box of label and value rows with a heading, the bank details an unpaid invoice is paid with.</summary>
/// <param name="Heading">The heading of the box.</param>
/// <param name="Rows">The rows, every value already set.</param>
internal sealed record PdfDetails(string Heading, IReadOnlyList<PdfRow> Rows);
