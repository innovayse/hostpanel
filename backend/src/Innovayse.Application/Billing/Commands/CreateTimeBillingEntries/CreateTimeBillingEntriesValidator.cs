namespace Innovayse.Application.Billing.Commands.CreateTimeBillingEntries;

using FluentValidation;

/// <summary>Validates <see cref="CreateTimeBillingEntriesCommand"/> before it reaches the handler.</summary>
public sealed class CreateTimeBillingEntriesValidator : AbstractValidator<CreateTimeBillingEntriesCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateTimeBillingEntriesValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.Entries).NotEmpty();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Description).NotEmpty().MaximumLength(500);
            entry.RuleFor(e => e.Hours).GreaterThan(0);
            entry.RuleFor(e => e.Rate).GreaterThanOrEqualTo(0);
        });
    }
}
