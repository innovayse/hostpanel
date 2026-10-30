using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoiceFulfilment;

/// <summary>Validates <see cref="ListInvoiceFulfilmentQuery"/>.</summary>
public sealed class ListInvoiceFulfilmentValidator : LocalizedValidator<ListInvoiceFulfilmentQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListInvoiceFulfilmentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
