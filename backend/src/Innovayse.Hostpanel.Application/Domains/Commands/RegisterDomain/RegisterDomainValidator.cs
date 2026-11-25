using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;

/// <summary>Validates <see cref="RegisterDomainCommand"/> inputs before the handler executes.</summary>
public sealed class RegisterDomainValidator : LocalizedValidator<RegisterDomainCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RegisterDomainValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["DomainClientIdRequired"]);

        // Any number of labels, not exactly two: "example.co.uk", "example.com.am" and
        // "shop.example.com" are all orderable. RegisterDomainCommand is also dispatched from
        // FulfillPaidOrderHandler against a name the customer has already paid for, so a name
        // this rule refused would fail *after* the money moved.
        RuleFor(x => x.DomainName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainNameRequired"])
            .MustBeDomainName(L["DomainNameInvalid"]);

        RuleFor(x => x.Years).InclusiveBetween(1, 10).WithMessage(L["DomainYearsRange"]);
    }
}
