using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOption;

/// <summary>Validates <see cref="DeleteProductOptionCommand"/> before it reaches the handler.</summary>
public sealed class DeleteProductOptionValidator : LocalizedValidator<DeleteProductOptionCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteProductOptionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductOptionIdInvalid"]);
    }
}
