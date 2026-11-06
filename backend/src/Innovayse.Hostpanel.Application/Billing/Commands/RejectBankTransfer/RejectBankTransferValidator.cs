using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RejectBankTransfer;

/// <summary>Validates <see cref="RejectBankTransferCommand"/> before it reaches the handler.</summary>
public sealed class RejectBankTransferValidator : LocalizedValidator<RejectBankTransferCommand>
{
    /// <summary>Maximum length of the note the client reads.</summary>
    private const int NoteMaxLength = 1000;

    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RejectBankTransferValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.Note)
            .NotEmpty()
            .MaximumLength(NoteMaxLength)
            .WithMessage(L["BankTransferRejectNoteInvalid", NoteMaxLength]);
    }
}
