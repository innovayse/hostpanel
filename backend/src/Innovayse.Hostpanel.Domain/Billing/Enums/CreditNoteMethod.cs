namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>How a <see cref="CreditNote"/> gives its value back to the client. Travels on the wire by name.</summary>
public enum CreditNoteMethod
{
    /// <summary>The value is put on the client's credit balance, where it shows in the credit history.</summary>
    AccountCredit = 0,

    /// <summary>The value is paid back through the gateway that took the money, as one refund of the invoice.</summary>
    GatewayRefund = 1,
}
