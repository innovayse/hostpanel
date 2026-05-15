namespace Innovayse.Application.Products.Commands.CreateProduct;

using FluentValidation;

/// <summary>Validates <see cref="CreateProductCommand"/> inputs.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>Initializes validation rules for product creation.</summary>
    public CreateProductValidator()
    {
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0);
    }
}
