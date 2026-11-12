using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListProductBundles;

/// <summary>
///     Validator for <see cref="ListProductBundlesQuery"/>.
///     The query carries no input, so no rule is declared: decided, not forgotten.
/// </summary>
public sealed class ListProductBundlesValidator : LocalizedValidator<ListProductBundlesQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListProductBundlesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
