namespace Innovayse.Application.Admin.Queries.GetRecentActivity;

/// <summary>The kinds of event the admin notification feed surfaces.</summary>
public enum ActivityEventType
{
    /// <summary>A new client account was created.</summary>
    ClientRegistered,

    /// <summary>An invoice's due date has passed without payment.</summary>
    InvoiceOverdue,

    /// <summary>A domain is approaching its expiry date.</summary>
    DomainExpiring,
}
