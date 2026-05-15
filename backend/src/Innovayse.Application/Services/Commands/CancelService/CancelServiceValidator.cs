namespace Innovayse.Application.Services.Commands.CancelService;

using FluentValidation;

/// <summary>Validates <see cref="CancelServiceCommand"/> inputs.</summary>
public sealed class CancelServiceValidator : AbstractValidator<CancelServiceCommand>
{
    /// <summary>Initializes validation rules for service cancellation.</summary>
    public CancelServiceValidator()
    {
        RuleFor(x => x.ServiceId).GreaterThan(0);
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => t is "Immediate" or "EndOfBillingPeriod")
            .WithMessage("Type must be 'Immediate' or 'EndOfBillingPeriod'.");
    }
}
