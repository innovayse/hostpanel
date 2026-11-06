using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>What a <see cref="CreditMovementType"/> means beyond its name.</summary>
public static class CreditMovementTypeExtensions
{
    /// <summary>
    /// Whether the client is told by email that the balance moved. The movements the client did not
    /// start and cannot see coming are announced; moving credit between the balance and an invoice is
    /// the client's own action or the renewal sweep's, and the invoice already says it.
    /// A credit note is mailed as the note itself, which already says what it put on the balance, so it sends no second mail.
    /// </summary>
    /// <param name="movement">The movement.</param>
    /// <returns><see langword="true"/> when the client is mailed the new balance.</returns>
    public static bool NotifiesClient(this CreditMovementType movement) =>
        movement is CreditMovementType.AdminAdd
            or CreditMovementType.AdminDeduct
            or CreditMovementType.RefundToCredit
            or CreditMovementType.TopUp
            or CreditMovementType.TopUpRefunded
            or CreditMovementType.PaymentSurplus;
}
