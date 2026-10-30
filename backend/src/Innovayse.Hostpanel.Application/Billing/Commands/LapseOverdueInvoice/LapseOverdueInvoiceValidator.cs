using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.LapseOverdueInvoice;

/// <summary>Validates <see cref="LapseOverdueInvoiceCommand"/>.</summary>
public sealed class LapseOverdueInvoiceValidator : LocalizedValidator<LapseOverdueInvoiceCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public LapseOverdueInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
