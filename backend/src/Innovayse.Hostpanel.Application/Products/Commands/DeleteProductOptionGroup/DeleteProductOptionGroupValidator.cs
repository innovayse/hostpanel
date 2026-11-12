using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOptionGroup;

/// <summary>Validates <see cref="DeleteProductOptionGroupCommand"/> before it reaches the handler.</summary>
public sealed class DeleteProductOptionGroupValidator : LocalizedValidator<DeleteProductOptionGroupCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteProductOptionGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductOptionGroupIdInvalid"]);
    }
}
