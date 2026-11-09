using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder;

/// <summary>Validates <see cref="FulfillInvoicedOrderCommand"/>: only the shape of the invoice id.</summary>
public sealed class FulfillInvoicedOrderValidator : LocalizedValidator<FulfillInvoicedOrderCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public FulfillInvoicedOrderValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
