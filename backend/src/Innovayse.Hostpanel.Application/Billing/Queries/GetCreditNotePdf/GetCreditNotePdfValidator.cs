using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNotePdf;

/// <summary>Validates <see cref="GetCreditNotePdfQuery"/>.</summary>
public sealed class GetCreditNotePdfValidator : LocalizedValidator<GetCreditNotePdfQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetCreditNotePdfValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CreditNoteId).MustBePositiveId(L["CreditNoteIdInvalid"]);
    }
}
