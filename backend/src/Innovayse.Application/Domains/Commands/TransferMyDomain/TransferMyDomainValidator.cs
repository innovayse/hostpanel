namespace Innovayse.Application.Domains.Commands.TransferMyDomain;

using FluentValidation;

/// <summary>Validates <see cref="TransferMyDomainCommand"/> inputs before the handler executes.</summary>
/// <remarks>
/// The same shape as <c>TransferDomainValidator</c> minus the client id, which this command does
/// not carry and must not. The year bound is stated here as well as in the TLD price table: a
/// period the table has no entry for is refused later anyway, but a caller who sends 0 or 500
/// should be told so before an order number is burned on it.
/// </remarks>
public sealed class TransferMyDomainValidator : AbstractValidator<TransferMyDomainCommand>
{
    /// <summary>Initialises validation rules for a client-initiated domain transfer-in.</summary>
    public TransferMyDomainValidator()
    {
        RuleFor(x => x.DomainName).NotEmpty().Must(name => name.Contains('.'));
        RuleFor(x => x.EppCode).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Years).InclusiveBetween(1, 10);
        RuleFor(x => x.PaymentMethod).NotEmpty();
    }
}
