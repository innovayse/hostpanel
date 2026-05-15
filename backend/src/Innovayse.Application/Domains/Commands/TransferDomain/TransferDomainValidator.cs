namespace Innovayse.Application.Domains.Commands.TransferDomain;

using FluentValidation;

/// <summary>Validates <see cref="TransferDomainCommand"/> inputs before the handler executes.</summary>
public sealed class TransferDomainValidator : AbstractValidator<TransferDomainCommand>
{
    /// <summary>Initializes validation rules for domain transfer.</summary>
    public TransferDomainValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.DomainName).NotEmpty();
        RuleFor(x => x.EppCode).NotEmpty().MinimumLength(6);
    }
}
