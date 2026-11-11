using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.MarkInvoiceOverdue;

/// <summary>Validates <see cref="MarkInvoiceOverdueCommand"/>.</summary>
public sealed class MarkInvoiceOverdueValidator : LocalizedValidator<MarkInvoiceOverdueCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public MarkInvoiceOverdueValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
