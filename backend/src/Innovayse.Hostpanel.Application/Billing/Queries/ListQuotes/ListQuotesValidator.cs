using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListQuotes;

/// <summary>
/// Validates <see cref="ListQuotesQuery"/> before it reaches the handler. The handler itself
/// clamps page and page size, so this exists only to document that the absence of stricter rules
/// was decided, not overlooked.
/// </summary>
public sealed class ListQuotesValidator : LocalizedValidator<ListQuotesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListQuotesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
