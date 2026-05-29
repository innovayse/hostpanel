namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using FluentValidation;

/// <summary>Validation rules for <see cref="CreateBillableItemCommand"/>.</summary>
public sealed class CreateBillableItemValidator : AbstractValidator<CreateBillableItemCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public CreateBillableItemValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0).WithMessage("Client ID must be positive.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Amount must be non-negative.");
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Type).Must(t => t == "OneTime" || t == "Recurring").WithMessage("Type must be OneTime or Recurring.");
        RuleFor(x => x.RecurringPeriod)
            .Must((cmd, period) => cmd.Type == "OneTime" || !string.IsNullOrEmpty(period))
            .WithMessage("RecurringPeriod is required for Recurring items.");
    }
}
