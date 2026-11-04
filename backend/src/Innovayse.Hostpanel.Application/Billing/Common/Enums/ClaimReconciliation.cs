namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>What looking an unfinished payment claim up at the gateway came to.</summary>
public enum ClaimReconciliation
{
    /// <summary>Nothing could be settled yet (the gateway did not answer, the charge is still pending, or the money is not accounted for); the claim stays held.</summary>
    StillOpen = 0,

    /// <summary>The gateway had taken the money and it is now recorded against the invoice.</summary>
    Settled = 1,

    /// <summary>The attempt is over and no money is owed to the invoice by it (declined, never charged, or the money was handed back); the claim is ended.</summary>
    Closed = 2,
}
