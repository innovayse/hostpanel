using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListCreditNotes;

/// <summary>
/// Validates <see cref="ListCreditNotesQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the invoice, client, status and method filters.
/// </summary>
public sealed class ListCreditNotesValidator : ListQueryValidator<ListCreditNotesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListCreditNotesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["CreditNoteStatusInvalid"]);
        RuleFor(x => x.Method).IsInEnum().WithMessage(L["CreditNoteMethodInvalid"]);
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.ClientId).MustBePositiveId(L["InvoiceClientIdInvalid"]);
    }
}
