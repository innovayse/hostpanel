namespace Innovayse.Application.Billing.Commands.DeleteBillableItem;

using FluentValidation;

/// <summary>Validation rules for <see cref="DeleteBillableItemCommand"/>.</summary>
public sealed class DeleteBillableItemValidator : AbstractValidator<DeleteBillableItemCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public DeleteBillableItemValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("ID must be positive.");
    }
}
