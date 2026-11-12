using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOptionGroup;

/// <summary>
/// Validates <see cref="UpdateProductOptionGroupCommand"/> before it reaches the handler. The rules that
/// compare fields, such as the quantity range against the type, stay in the aggregate.
/// </summary>
public sealed class UpdateProductOptionGroupValidator : LocalizedValidator<UpdateProductOptionGroupCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateProductOptionGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductOptionGroupIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductOptionGroupNameRequired"])
            .MaximumLength(ProductOptionGroup.NameMaxLength)
            .WithMessage(L["ProductOptionGroupNameTooLong", ProductOptionGroup.NameMaxLength]);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage(L["ProductOptionDisplayOrderInvalid"]);
    }
}
