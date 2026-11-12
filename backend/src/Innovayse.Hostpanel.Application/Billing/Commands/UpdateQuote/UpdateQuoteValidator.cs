using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.UpdateQuote;

/// <summary>Validates <see cref="UpdateQuoteCommand"/> before it reaches the handler.</summary>
public sealed class UpdateQuoteValidator : LocalizedValidator<UpdateQuoteCommand>
{
    /// <summary>Maximum length of the quote subject.</summary>
    private const int SubjectMaxLength = 500;

    /// <summary>Maximum length of a line item description.</summary>
    private const int DescriptionMaxLength = 500;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateQuoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.QuoteId).MustBePositiveId(L["QuoteIdInvalid"]);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(SubjectMaxLength).WithMessage(L["QuoteSubjectInvalid"]);
        RuleFor(x => x.Stage).IsInEnum().WithMessage(L["QuoteStageInvalid"]);
        RuleFor(x => x.Items).NotNull().WithMessage(L["QuoteItemsRequired"]);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description)
                .NotEmpty()
                .MaximumLength(DescriptionMaxLength)
                .WithMessage(L["QuoteItemDescriptionInvalid", DescriptionMaxLength]);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage(L["QuoteItemUnitPriceInvalid"]);
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage(L["QuoteItemQuantityInvalid"]);
            item.RuleFor(i => i.DiscountPercent).MustBeDiscountPercentRange(L["QuoteItemDiscountInvalid"]);
        });
    }
}
