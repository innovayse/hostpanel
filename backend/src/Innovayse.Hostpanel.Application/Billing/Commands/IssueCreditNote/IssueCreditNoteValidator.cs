using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.IssueCreditNote;

/// <summary>Validates <see cref="IssueCreditNoteCommand"/>.</summary>
public sealed class IssueCreditNoteValidator : LocalizedValidator<IssueCreditNoteCommand>
{
    /// <summary>The most lines one credit note carries.</summary>
    private const int MaxItems = 100;

    /// <summary>The longest line description the table holds.</summary>
    private const int MaxDescriptionLength = 500;

    /// <summary>The most units one line may credit.</summary>
    private const int MaxQuantity = 100_000;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public IssueCreditNoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.Method).IsInEnum().WithMessage(L["CreditNoteMethodInvalid"]);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(L["CreditNoteReasonRequired"])
            .MaximumLength(CreditNote.MaxReasonLength).WithMessage(L["CreditNoteReasonTooLong"]);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage(L["CreditNoteItemsRequired"])
            .Must(items => items is null || items.Count <= MaxItems).WithMessage(L["CreditNoteItemsTooMany"]);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description)
                .NotEmpty().WithMessage(L["CreditNoteItemDescriptionInvalid"])
                .MaximumLength(MaxDescriptionLength).WithMessage(L["CreditNoteItemDescriptionInvalid"]);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0).WithMessage(L["CreditNoteItemPriceInvalid"]);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, MaxQuantity).WithMessage(L["CreditNoteItemQuantityInvalid"]);
        });
    }
}
