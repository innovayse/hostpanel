namespace Innovayse.Application.Domains.Commands.RenewDomain;

using FluentValidation;

/// <summary>Validates <see cref="RenewDomainCommand"/> inputs before the handler executes.</summary>
public sealed class RenewDomainValidator : AbstractValidator<RenewDomainCommand>
{
    /// <summary>Initializes validation rules for domain renewal.</summary>
    public RenewDomainValidator()
    {
        RuleFor(x => x.DomainId).GreaterThan(0);
        RuleFor(x => x.Years).InclusiveBetween(1, 10);
    }
}
