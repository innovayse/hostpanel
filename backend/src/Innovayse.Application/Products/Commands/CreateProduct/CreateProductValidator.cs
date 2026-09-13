namespace Innovayse.Application.Products.Commands.CreateProduct;

using FluentValidation;
using Innovayse.Application.Products.Common;

/// <summary>Validates <see cref="CreateProductCommand"/> inputs.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>Initializes validation rules for product creation.</summary>
    public CreateProductValidator()
    {
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.PackageName).MaximumLength(100).When(x => x.PackageName is not null);
        // A new product must be sellable somewhere; an update may clear every price (see UpdateProductValidator).
        RuleFor(x => x.Prices).NotEmpty().WithMessage("A new product needs at least one price.");
        RuleForEach(x => x.Prices).SetValidator(new ProductPriceInputValidator());
    }
}
