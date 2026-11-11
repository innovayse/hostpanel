using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DuplicateQuote;

/// <summary>Validates <see cref="DuplicateQuoteCommand"/> before it reaches the handler.</summary>
public sealed class DuplicateQuoteValidator : LocalizedValidator<DuplicateQuoteCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DuplicateQuoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.QuoteId).MustBePositiveId(L["QuoteIdInvalid"]);
    }
}
