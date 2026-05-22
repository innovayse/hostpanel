namespace Innovayse.Application.Billing.Commands.CreateClientTransaction;

using FluentValidation;

/// <summary>Validates <see cref="CreateClientTransactionCommand"/> before it reaches the handler.</summary>
public sealed class CreateClientTransactionValidator : AbstractValidator<CreateClientTransactionCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateClientTransactionValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.AmountIn).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AmountOut).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fees).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.AmountIn > 0 || x.AmountOut > 0)
            .WithMessage("At least one of AmountIn or AmountOut must be greater than 0.");
    }
}
