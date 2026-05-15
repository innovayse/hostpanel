namespace Innovayse.Application.Services.Commands.TerminateService;

using FluentValidation;

/// <summary>Validates <see cref="TerminateServiceCommand"/> inputs.</summary>
public sealed class TerminateServiceValidator : AbstractValidator<TerminateServiceCommand>
{
    /// <summary>Initializes validation rules for service termination.</summary>
    public TerminateServiceValidator()
    {
        RuleFor(x => x.ServiceId).GreaterThan(0);
    }
}
