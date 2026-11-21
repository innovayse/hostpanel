using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListAvailablePaymentMethods;

/// <summary>
///     Validator for <see cref="ListAvailablePaymentMethodsQuery"/>. A currency code puts no rule on the query: any
///     string is a syntactically valid ISO 4217 code to try, and a code the panel does not offer is answered with an
///     empty list by the handler rather than refused. The cart's products are bounded, since the route is open to guests
///     and each id is looked up.
/// </summary>
public sealed class ListAvailablePaymentMethodsValidator : LocalizedValidator<ListAvailablePaymentMethodsQuery>
{
    /// <summary>Most products a cart may name; far above any real order, it only stops a request that asks for a huge lookup.</summary>
    private const int MaxProductIds = 100;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListAvailablePaymentMethodsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductIds).Must(ids => ids is null || ids.Count <= MaxProductIds)
            .WithMessage(L["ListAvailablePaymentMethodsTooManyProducts", MaxProductIds]);
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
