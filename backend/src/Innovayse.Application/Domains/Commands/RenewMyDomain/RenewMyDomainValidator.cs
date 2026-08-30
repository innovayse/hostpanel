namespace Innovayse.Application.Domains.Commands.RenewMyDomain;

using FluentValidation;

/// <summary>Validates <see cref="RenewMyDomainCommand"/> inputs before the handler executes.</summary>
/// <remarks>
/// The same shape as <c>TransferMyDomainValidator</c>, for the same reasons. The year bound is
/// stated here as well as in the TLD price table: a period the table has no entry for is refused
/// later anyway, but a caller who sends 0 or 500 should be told so before an order number is
/// burned on it. There is no rule about who owns the domain -- that is
/// <c>IDomainOwnership</c>'s, in the handler, because it needs the credential.
/// </remarks>
public sealed class RenewMyDomainValidator : AbstractValidator<RenewMyDomainCommand>
{
    /// <summary>Initialises validation rules for a client-initiated domain renewal.</summary>
    public RenewMyDomainValidator()
    {
        RuleFor(x => x.DomainId).GreaterThan(0);
        RuleFor(x => x.Years).InclusiveBetween(1, 10);
        RuleFor(x => x.PaymentMethod).NotEmpty();
    }
}
