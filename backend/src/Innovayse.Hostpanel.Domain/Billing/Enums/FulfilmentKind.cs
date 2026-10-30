namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>
/// What happened to an invoice that a fulfilment plugin is told about. The values are also the order in
/// which one plugin is told about one invoice: paid before refunded before lapsed.
/// </summary>
public enum FulfilmentKind
{
    /// <summary>The invoice was paid; the plugin delivers what was sold.</summary>
    Paid = 0,

    /// <summary>The invoice was refunded, in full or in part; the plugin takes back what the refund covers.</summary>
    Refunded = 1,

    /// <summary>The invoice stayed overdue past its grace period; the plugin ends what the unpaid period would have covered.</summary>
    Lapsed = 2,
}
