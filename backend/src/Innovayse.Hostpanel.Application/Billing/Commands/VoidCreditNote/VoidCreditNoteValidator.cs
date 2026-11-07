using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.VoidCreditNote;

/// <summary>Validates <see cref="VoidCreditNoteCommand"/>.</summary>
public sealed class VoidCreditNoteValidator : LocalizedValidator<VoidCreditNoteCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public VoidCreditNoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CreditNoteId).MustBePositiveId(L["CreditNoteIdInvalid"]);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(L["CreditNoteVoidReasonRequired"])
            .MaximumLength(CreditNote.MaxReasonLength).WithMessage(L["CreditNoteReasonTooLong"]);
    }
}
