namespace Innovayse.Application.Billing.Commands.RemovePaymentMethod;

using FluentValidation;

/// <summary>Validates <see cref="RemovePaymentMethodCommand"/>.</summary>
public sealed class RemovePaymentMethodValidator : AbstractValidator<RemovePaymentMethodCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public RemovePaymentMethodValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
    }
}
