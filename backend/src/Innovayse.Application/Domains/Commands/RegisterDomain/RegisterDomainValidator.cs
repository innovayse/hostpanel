namespace Innovayse.Application.Domains.Commands.RegisterDomain;

using FluentValidation;

/// <summary>Validates <see cref="RegisterDomainCommand"/> inputs before the handler executes.</summary>
public sealed class RegisterDomainValidator : AbstractValidator<RegisterDomainCommand>
{
    /// <summary>Initializes validation rules for domain registration.</summary>
    public RegisterDomainValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);

        // Any number of labels, not exactly two. The previous pattern was
        // `^[a-zA-Z0-9\-]+\.[a-zA-Z]{2,}$`, which accepts "example.com" and rejects
        // "example.co.uk", "example.com.am" and "shop.example.com" -- all of them orderable
        // today, because nothing ran this rule. RegisterDomainCommand is also dispatched from
        // FulfillPaidOrderHandler against a name the customer has already paid for, so a name
        // this rule refused would fail *after* the money moved.
        RuleFor(x => x.DomainName)
            .NotEmpty()
            .Matches(@"^(?=.{1,253}$)[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}$")
            .WithMessage("DomainName must be a valid fully-qualified domain name (e.g. 'example.com').");

        RuleFor(x => x.Years).InclusiveBetween(1, 10);
    }
}
