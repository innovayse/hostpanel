using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyStoredPaymentMethods;

/// <summary>
/// Validator for <see cref="GetMyStoredPaymentMethodsQuery"/>. The query carries nothing the caller chooses, so no rule
/// is declared here; decided, not forgotten.
/// </summary>
public sealed class GetMyStoredPaymentMethodsValidator : LocalizedValidator<GetMyStoredPaymentMethodsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyStoredPaymentMethodsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
