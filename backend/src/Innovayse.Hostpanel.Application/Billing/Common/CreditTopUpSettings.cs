namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The limits the admin sets on one credit top-up, in the client's billing currency.</summary>
/// <param name="Minimum">The least a client may add in one top-up.</param>
/// <param name="Maximum">The most a client may add in one top-up.</param>
/// <param name="MaxOpen">The most unpaid top-up invoices a client may hold at once.</param>
public sealed record CreditTopUpSettings(decimal Minimum, decimal Maximum, int MaxOpen)
{
    /// <summary>Whether an amount is inside the limits, both ends included.</summary>
    /// <param name="amount">The amount a client wants to add.</param>
    /// <returns><see langword="true"/> when the amount is neither below the minimum nor above the maximum.</returns>
    public bool Allows(decimal amount) => amount >= Minimum && amount <= Maximum;
}
