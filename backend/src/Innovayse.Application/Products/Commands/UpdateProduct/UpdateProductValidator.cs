namespace Innovayse.Application.Products.Commands.UpdateProduct;

using FluentValidation;
using Innovayse.Application.Products.Common;

/// <summary>Validates <see cref="UpdateProductCommand"/> inputs.</summary>
public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    /// <summary>Initializes validation rules for a product update.</summary>
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.PackageName).MaximumLength(100).When(x => x.PackageName is not null);
        // An update may legitimately clear every price: the product becomes unsellable and the
        // admin grid shows it as such. Only the entries that are present are checked.
        RuleFor(x => x.Prices).NotNull();
        RuleForEach(x => x.Prices).SetValidator(new ProductPriceInputValidator());
    }
}
