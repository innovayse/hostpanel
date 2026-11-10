using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyInvoice;

/// <summary>
/// Validator for <see cref="GetMyInvoiceQuery"/>. Checks only the shape of the id; whether it
/// names an invoice the caller may read is the handler's answer.
/// </summary>
public sealed class GetMyInvoiceValidator : LocalizedValidator<GetMyInvoiceQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
