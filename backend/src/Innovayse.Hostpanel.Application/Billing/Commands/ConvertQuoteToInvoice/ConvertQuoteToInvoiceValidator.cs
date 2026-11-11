using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ConvertQuoteToInvoice;

/// <summary>Validates <see cref="ConvertQuoteToInvoiceCommand"/> before it reaches the handler.</summary>
public sealed class ConvertQuoteToInvoiceValidator : LocalizedValidator<ConvertQuoteToInvoiceCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ConvertQuoteToInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.QuoteId).MustBePositiveId(L["QuoteIdInvalid"]);
    }
}
