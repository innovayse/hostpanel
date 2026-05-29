namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using FluentValidation;
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
    }
}
