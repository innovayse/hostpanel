using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductBundle;

/// <summary>Validates <see cref="DeleteProductBundleCommand"/> before it reaches the handler.</summary>
public sealed class DeleteProductBundleValidator : LocalizedValidator<DeleteProductBundleCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteProductBundleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductBundleIdInvalid"]);
    }
}
