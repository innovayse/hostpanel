using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for <see cref="CreditMovementTypeExtensions"/>: which movements the client is mailed about.</summary>
public sealed class CreditMovementTypeExtensionsTests
{
    /// <summary>The movements the client did not start and cannot see coming are announced.</summary>
    /// <param name="movement">An announced movement.</param>
    [Theory]
    [InlineData(CreditMovementType.AdminAdd)]
    [InlineData(CreditMovementType.AdminDeduct)]
    [InlineData(CreditMovementType.RefundToCredit)]
    [InlineData(CreditMovementType.TopUp)]
    [InlineData(CreditMovementType.TopUpRefunded)]
    [InlineData(CreditMovementType.PaymentSurplus)]
    [InlineData(CreditMovementType.PlanChangeCredit)]
    public void NotifiesClient_IsTrueForTheMovementsTheClientDidNotStart(CreditMovementType movement)
    {
        movement.NotifiesClient().Should().BeTrue();
    }

    /// <summary>Moving credit to or from an invoice is the client own act or the renewal sweep, and the invoice says it; a credit note is mailed as the note itself.</summary>
    /// <param name="movement">A quiet movement.</param>
    [Theory]
    [InlineData(CreditMovementType.InvoiceApplied)]
    [InlineData(CreditMovementType.InvoiceReturned)]
    [InlineData(CreditMovementType.RenewalAutoApplied)]
    [InlineData(CreditMovementType.TransactionReversed)]
    [InlineData(CreditMovementType.CreditNoteIssued)]
    [InlineData(CreditMovementType.CreditNoteVoided)]
    public void NotifiesClient_IsFalseForMovementsTheInvoiceOrTheCreditNoteAlreadySay(CreditMovementType movement)
    {
        movement.NotifiesClient().Should().BeFalse();
    }

    /// <summary>Every defined movement is decided one way or the other, so a new one cannot be added without a thought.</summary>
    [Fact]
    public void EveryDefinedMovement_HasAnAnswer()
    {
        var announced = new[]
        {
            CreditMovementType.AdminAdd, CreditMovementType.AdminDeduct, CreditMovementType.RefundToCredit,
            CreditMovementType.TopUp, CreditMovementType.TopUpRefunded, CreditMovementType.PaymentSurplus,
            CreditMovementType.PlanChangeCredit,
        };

        foreach (var movement in Enum.GetValues<CreditMovementType>())
        {
            movement.NotifiesClient().Should().Be(announced.Contains(movement), movement.ToString());
        }
    }
}
