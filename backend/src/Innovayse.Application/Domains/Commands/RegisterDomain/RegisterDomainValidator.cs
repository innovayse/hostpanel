namespace Innovayse.Application.Domains.Commands.RegisterDomain;

using FluentValidation;

/// <summary>Validates <see cref="RegisterDomainCommand"/> inputs before the handler executes.</summary>
public sealed class RegisterDomainValidator : AbstractValidator<RegisterDomainCommand>
{
    /// <summary>Initializes validation rules for domain registration.</summary>
    public RegisterDomainValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);

        RuleFor(x => x.DomainName)
            .NotEmpty()
            .Matches(@"^[a-zA-Z0-9\-]+\.[a-zA-Z]{2,}$")
            .WithMessage("DomainName must be a valid fully-qualified domain name (e.g. 'example.com').");

        RuleFor(x => x.Years).InclusiveBetween(1, 10);
    }
}
