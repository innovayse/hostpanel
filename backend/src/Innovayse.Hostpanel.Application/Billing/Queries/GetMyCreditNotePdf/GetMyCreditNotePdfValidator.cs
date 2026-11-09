using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyCreditNotePdf;

/// <summary>Validates <see cref="GetMyCreditNotePdfQuery"/>.</summary>
public sealed class GetMyCreditNotePdfValidator : LocalizedValidator<GetMyCreditNotePdfQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyCreditNotePdfValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CreditNoteId).MustBePositiveId(L["CreditNoteIdInvalid"]);
    }
}
