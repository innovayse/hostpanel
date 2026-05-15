namespace Innovayse.Application.Services.Commands.SuspendService;

using FluentValidation;

/// <summary>Validates <see cref="SuspendServiceCommand"/> inputs.</summary>
public sealed class SuspendServiceValidator : AbstractValidator<SuspendServiceCommand>
{
    /// <summary>Initializes validation rules for service suspension.</summary>
    public SuspendServiceValidator()
    {
        RuleFor(x => x.ServiceId).GreaterThan(0);
    }
}
