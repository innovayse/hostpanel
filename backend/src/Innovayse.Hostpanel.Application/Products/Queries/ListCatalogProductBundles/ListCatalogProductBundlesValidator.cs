using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListCatalogProductBundles;

/// <summary>
///     Validator for <see cref="ListCatalogProductBundlesQuery"/>.
///     The query carries no input, so no rule is declared: decided, not forgotten.
/// </summary>
public sealed class ListCatalogProductBundlesValidator : LocalizedValidator<ListCatalogProductBundlesQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListCatalogProductBundlesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
