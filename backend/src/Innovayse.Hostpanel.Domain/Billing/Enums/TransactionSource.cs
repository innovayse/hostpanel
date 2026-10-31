namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>
/// Says where a client ledger <see cref="Transaction"/> came from. The payment method alone cannot:
/// a manual entry and an import may carry any method, even a gateway's name, and may repeat a
/// reference, which a gateway payment never does.
/// </summary>
public enum TransactionSource
{
    /// <summary>Money a payment gateway or card processor took, recorded when a gateway payment settled (or was handed back). Its method and reference are unique.</summary>
    GatewayPayment = 0,

    /// <summary>Money a payment gateway returned to the payer, recorded when a gateway refund was issued.</summary>
    GatewayRefund = 1,

    /// <summary>An entry staff made by hand: a recorded payment, a refund that moved no gateway money, a bulk mark-paid, or a free-form ledger row.</summary>
    Manual = 2,

    /// <summary>A row copied in by the migration importers from another system, whose references the source owns.</summary>
    Import = 3,
}
