namespace Innovayse.Application.Billing.Commands.SetDefaultPaymentMethod;

using FluentValidation;

/// <summary>Validates <see cref="SetDefaultPaymentMethodCommand"/>.</summary>
public sealed class SetDefaultPaymentMethodValidator : AbstractValidator<SetDefaultPaymentMethodCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public SetDefaultPaymentMethodValidator()
    {
        RuleFor(x => x.PaymentMethodId).NotEmpty();
    }
}
