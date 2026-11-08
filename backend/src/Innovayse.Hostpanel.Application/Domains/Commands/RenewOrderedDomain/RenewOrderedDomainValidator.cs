using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;

/// <summary>Validates <see cref="RenewOrderedDomainCommand"/>.</summary>
public sealed class RenewOrderedDomainValidator : LocalizedValidator<RenewOrderedDomainCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RenewOrderedDomainValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.DomainName).NotEmpty().WithMessage(L["DomainNameRequired"]);
        RuleFor(x => x.Years).InclusiveBetween(1, 10).WithMessage(L["DomainYearsRange"]);
        RuleFor(x => x.OrderItemId).GreaterThan(0).WithMessage(L["OrderItemIdInvalid"]).When(x => x.OrderItemId is not null);
    }
}
