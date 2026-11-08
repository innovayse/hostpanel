using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.RenewDomain;

/// <summary>Validates <see cref="RenewDomainCommand"/> inputs before the handler executes.</summary>
public sealed class RenewDomainValidator : LocalizedValidator<RenewDomainCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RenewDomainValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).GreaterThan(0).WithMessage(L["DomainIdRequired"]);
        RuleFor(x => x.Years).InclusiveBetween(1, 10).WithMessage(L["DomainYearsRange"]);
        RuleFor(x => x.OrderItemId).GreaterThan(0).WithMessage(L["OrderItemIdInvalid"]).When(x => x.OrderItemId is not null);
    }
}
