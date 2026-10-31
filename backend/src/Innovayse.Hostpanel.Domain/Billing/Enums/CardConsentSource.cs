namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>How a client agreed that a saved card may be charged without them.</summary>
public enum CardConsentSource
{
    /// <summary>The client ticked the box offering to keep the card while paying an invoice.</summary>
    PaymentCheckbox = 0,

    /// <summary>The client added the card on purpose through the "Add card" flow.</summary>
    AddCard = 1,
}
