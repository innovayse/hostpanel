using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.CheckProductLimits;

/// <summary>Validates <see cref="CheckProductLimitsQuery"/>.</summary>
public sealed class CheckProductLimitsValidator : LocalizedValidator<CheckProductLimitsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CheckProductLimitsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductIds).NotNull();
        RuleForEach(x => x.ProductIds).GreaterThan(0).WithMessage(L["ProductIdInvalid"]);
        RuleFor(x => x.ClientId).GreaterThan(0).When(x => x.ClientId is not null).WithMessage(L["ClientIdInvalid"]);
    }
}
