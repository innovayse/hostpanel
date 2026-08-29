namespace Innovayse.Application.Billing.Commands.CreateQuote;

using FluentValidation;

/// <summary>Validation rules for <see cref="QuoteItemRequest"/>.</summary>
public sealed class QuoteItemRequestValidator : AbstractValidator<QuoteItemRequest>
{
    /// <summary>Initializes validation rules.</summary>
    public QuoteItemRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price must be non-negative.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
    }
}
