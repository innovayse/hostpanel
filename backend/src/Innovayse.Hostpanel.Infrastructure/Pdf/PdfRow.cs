namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>A label and its value, one row of the meta block, the totals or the bank details.</summary>
/// <param name="Label">The muted text on the left.</param>
/// <param name="Value">The text on the right, already formatted.</param>
/// <param name="Strong">Whether the row is set large and bold, as the amount due is.</param>
/// <param name="RuleAbove">Whether a thin rule is drawn above the row.</param>
internal sealed record PdfRow(string Label, string Value, bool Strong = false, bool RuleAbove = false);
