using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientQuotes;

/// <summary>
/// Validates <see cref="ListClientQuotesQuery"/>. The page, page size, search and the stage and validity filters are the
/// shared quote list rules (<see cref="QuoteListQueryValidator{T}"/>); this adds the rule of the client id.
/// </summary>
public sealed class ListClientQuotesValidator : QuoteListQueryValidator<ListClientQuotesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientQuotesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ListClientIdInvalid"]);
    }
}
