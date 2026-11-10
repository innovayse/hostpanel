using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetQuote;

/// <summary>Validates <see cref="GetQuoteQuery"/> before it reaches the handler.</summary>
public sealed class GetQuoteValidator : LocalizedValidator<GetQuoteQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetQuoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["QuoteIdInvalid"]);
    }
}
