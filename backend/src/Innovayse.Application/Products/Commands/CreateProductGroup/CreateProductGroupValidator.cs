namespace Innovayse.Application.Products.Commands.CreateProductGroup;

using FluentValidation;

/// <summary>Validates <see cref="CreateProductGroupCommand"/> inputs.</summary>
public sealed class CreateProductGroupValidator : AbstractValidator<CreateProductGroupCommand>
{
    /// <summary>Initializes validation rules for product group creation.</summary>
    public CreateProductGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
