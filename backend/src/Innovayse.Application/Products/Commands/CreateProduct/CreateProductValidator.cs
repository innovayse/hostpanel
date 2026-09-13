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
        // A new product must be sellable somewhere; an update may clear every price (see
        // UpdateProductValidator). Without a per-currency list the legacy pair must carry a figure.
        RuleFor(x => x.Prices).NotEmpty().When(x => x.Prices is not null)
            .WithMessage("A new product needs at least one price.");
        RuleFor(x => x.MonthlyPrice).NotNull().When(x => x.Prices is null && x.AnnualPrice is null)
            .WithMessage("A new product needs at least one price.");
        RuleForEach(x => x.Prices).SetValidator(new ProductPriceInputValidator()).When(x => x.Prices is not null);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0).When(x => x.MonthlyPrice is not null);
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0).When(x => x.AnnualPrice is not null);
    }
}
