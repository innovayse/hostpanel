using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateBillableItem;

/// <summary>Validates <see cref="CreateBillableItemCommand"/> before it reaches the handler.</summary>
public sealed class CreateBillableItemValidator : LocalizedValidator<CreateBillableItemCommand>
{
    /// <summary>Maximum length of a description.</summary>
    private const int DescriptionMaxLength = 500;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateBillableItemValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["BillableItemClientIdInvalid"]);
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(DescriptionMaxLength)
            .WithMessage(L["BillableItemDescriptionInvalid", DescriptionMaxLength]);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage(L["BillableItemAmountInvalid"]);
        RuleFor(x => x.Currency).MustBeCurrencyCode(L["BillableItemCurrencyInvalid"]);
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["BillableItemTypeInvalid"]);
        RuleFor(x => x.RecurringPeriod)
            .Must((cmd, period) => cmd.Type == BillableItemType.OneTime || !string.IsNullOrEmpty(period))
            .WithMessage(L["BillableItemRecurringPeriodRequired"]);
    }
}
