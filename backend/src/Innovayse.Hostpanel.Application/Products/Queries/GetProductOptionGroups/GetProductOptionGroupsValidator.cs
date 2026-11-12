using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductOptionGroups;

/// <summary>Validates <see cref="GetProductOptionGroupsQuery"/> before it reaches the handler.</summary>
public sealed class GetProductOptionGroupsValidator : LocalizedValidator<GetProductOptionGroupsQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetProductOptionGroupsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductId).MustBePositiveId(L["ProductIdInvalid"]);
    }
}
