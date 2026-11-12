using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProductOptions;

/// <summary>Validates <see cref="GetOrderableProductOptionsQuery"/> before it reaches the handler.</summary>
public sealed class GetOrderableProductOptionsValidator : LocalizedValidator<GetOrderableProductOptionsQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetOrderableProductOptionsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductId).MustBePositiveId(L["ProductIdInvalid"]);
    }
}
