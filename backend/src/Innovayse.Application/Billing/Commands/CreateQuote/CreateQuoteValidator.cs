namespace Innovayse.Application.Billing.Commands.CreateQuote;

using FluentValidation;

/// <summary>Validation rules for <see cref="CreateQuoteCommand"/>.</summary>
public sealed class CreateQuoteValidator : AbstractValidator<CreateQuoteCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public CreateQuoteValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0).WithMessage("Client ID must be positive.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ExpiryDate).NotEmpty();
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Quote must have at least one line item.")
            .Must(items => items.Count > 0).WithMessage("Quote must have at least one line item.");

        RuleForEach(x => x.Items).SetValidator(new QuoteItemRequestValidator());
    }
}

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
