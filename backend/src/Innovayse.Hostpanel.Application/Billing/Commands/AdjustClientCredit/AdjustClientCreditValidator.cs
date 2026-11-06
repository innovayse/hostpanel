using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.AdjustClientCredit;

/// <summary>Validates <see cref="AdjustClientCreditCommand"/>.</summary>
public sealed class AdjustClientCreditValidator : LocalizedValidator<AdjustClientCreditCommand>
{
    /// <summary>The longest reason the credit history line holds.</summary>
    private const int MaxReasonLength = 500;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AdjustClientCreditValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["TransactionClientIdInvalid"]);
        RuleFor(x => x.Direction).IsInEnum().WithMessage(L["CreditAdjustmentDirectionInvalid"]);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage(L["CreditAdjustmentAmountInvalid"]);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(L["CreditAdjustmentReasonRequired"])
            .MaximumLength(MaxReasonLength).WithMessage(L["CreditAdjustmentReasonTooLong"]);
    }
}
