using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoicePaymentClaims;

/// <summary>
///     Validator for ListInvoicePaymentClaimsQuery. Page and page size are clamped by the handler, so no
///     rule is declared for them here; the status filter must name a known claim status.
/// </summary>
public sealed class ListInvoicePaymentClaimsValidator : LocalizedValidator<ListInvoicePaymentClaimsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListInvoicePaymentClaimsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["InvoicePaymentClaimStatusInvalid"]);
    }
}
