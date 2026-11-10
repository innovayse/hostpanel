using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetActiveDomainProduct;

/// <summary>
/// Validates <see cref="GetActiveDomainProductQuery"/>. The query carries no input, so no rule is
/// declared; decided, not forgotten.
/// </summary>
public sealed class GetActiveDomainProductValidator : LocalizedValidator<GetActiveDomainProductQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetActiveDomainProductValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
