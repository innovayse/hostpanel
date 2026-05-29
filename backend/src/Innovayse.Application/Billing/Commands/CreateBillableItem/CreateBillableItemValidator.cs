namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using FluentValidation;
<<<<<<< HEAD

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
=======
using Innovayse.Domain.Billing;

/// <summary>Validates <see cref="CreateBillableItemCommand"/> before it reaches the handler.</summary>
public sealed class CreateBillableItemValidator : AbstractValidator<CreateBillableItemCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateBillableItemValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HoursQty).GreaterThanOrEqualTo(0);

        When(x => x.InvoiceAction == InvoiceAction.Recur, () =>
        {
            RuleFor(x => x.RecurrenceInterval)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("'Recurrence Interval' must be greater than 0 for recurring items.");
            RuleFor(x => x.RecurrencePeriod)
                .NotNull()
                .WithMessage("'Recurrence Period' is required for recurring items.");
        });
>>>>>>> origin/main
    }
}
