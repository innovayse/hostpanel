using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNote;

/// <summary>Validates <see cref="GetCreditNoteQuery"/>.</summary>
public sealed class GetCreditNoteValidator : LocalizedValidator<GetCreditNoteQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetCreditNoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CreditNoteId).MustBePositiveId(L["CreditNoteIdInvalid"]);
    }
}
