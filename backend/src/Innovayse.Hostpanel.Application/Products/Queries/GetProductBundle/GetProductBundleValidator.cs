using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductBundle;

/// <summary>Validates <see cref="GetProductBundleQuery"/> before it reaches the handler.</summary>
public sealed class GetProductBundleValidator : LocalizedValidator<GetProductBundleQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetProductBundleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductBundleIdInvalid"]);
    }
}
