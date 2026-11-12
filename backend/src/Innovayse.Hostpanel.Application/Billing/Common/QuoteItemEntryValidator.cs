using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Validates <see cref="QuoteItemEntry"/> before it reaches the handler.</summary>
public sealed class QuoteItemEntryValidator : LocalizedValidator<QuoteItemEntry>
{
    /// <summary>Maximum length of a line item description.</summary>
    private const int DescriptionMaxLength = 500;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public QuoteItemEntryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(DescriptionMaxLength)
            .WithMessage(L["QuoteItemDescriptionInvalid", DescriptionMaxLength]);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage(L["QuoteItemUnitPriceInvalid"]);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(L["QuoteItemQuantityInvalid"]);
        RuleFor(x => x.DiscountPercent).MustBeDiscountPercentRange(L["QuoteItemDiscountInvalid"]);
    }
}
