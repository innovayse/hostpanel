using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListCreditNotes;

/// <summary>Validates <see cref="ListCreditNotesQuery"/>. Page and page size are clamped by the handler.</summary>
public sealed class ListCreditNotesValidator : LocalizedValidator<ListCreditNotesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListCreditNotesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["CreditNoteStatusInvalid"]);
        RuleFor(x => x.InvoiceId).Must(id => id is null or > 0).WithMessage(L["InvoiceIdInvalid"]);
        RuleFor(x => x.ClientId).Must(id => id is null or > 0).WithMessage(L["TransactionClientIdInvalid"]);
    }
}
