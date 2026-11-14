using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListQuotes;

/// <summary>
/// Validates <see cref="ListQuotesQuery"/>. The page, page size, search and the stage and validity filters are the shared
/// quote list rules (<see cref="QuoteListQueryValidator{T}"/>); this adds the rule of the client filter.
/// </summary>
public sealed class ListQuotesValidator : QuoteListQueryValidator<ListQuotesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListQuotesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["QuoteClientIdInvalid"]);
    }
}
