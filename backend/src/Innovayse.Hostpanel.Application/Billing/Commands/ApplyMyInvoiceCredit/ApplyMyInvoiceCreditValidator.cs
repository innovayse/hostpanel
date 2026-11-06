using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyMyInvoiceCredit;

/// <summary>Validates <see cref="ApplyMyInvoiceCreditCommand"/>.</summary>
public sealed class ApplyMyInvoiceCreditValidator : LocalizedValidator<ApplyMyInvoiceCreditCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ApplyMyInvoiceCreditValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage(L["InvoiceCreditAmountInvalid"]);
    }
}
