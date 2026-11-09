using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductFeature;

/// <summary>Validates <see cref="DeleteProductFeatureCommand"/> before it reaches the handler.</summary>
public sealed class DeleteProductFeatureValidator : LocalizedValidator<DeleteProductFeatureCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteProductFeatureValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductFeatureIdInvalid"]);
    }
}
