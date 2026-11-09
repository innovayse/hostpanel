using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DeleteQuote;

/// <summary>Validates <see cref="DeleteQuoteCommand"/> before it reaches the handler.</summary>
public sealed class DeleteQuoteValidator : LocalizedValidator<DeleteQuoteCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteQuoteValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.QuoteId).MustBePositiveId(L["QuoteIdInvalid"]);
    }
}
