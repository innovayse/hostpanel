namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>What the enabled fulfilment plugins let an admin refund of an invoice right now.</summary>
/// <param name="Amount">
/// The amount to refund: what was asked for, or the smallest cap a plugin advised when that is less.
/// </param>
/// <param name="Notes">
/// The operator's own notes followed by one sentence per plugin that capped the refund, cut to
/// <see cref="RefundAdviser.NotesMaxLength"/>; <see langword="null"/> when there is nothing to record.
/// </param>
public sealed record RefundCap(decimal Amount, string? Notes);
