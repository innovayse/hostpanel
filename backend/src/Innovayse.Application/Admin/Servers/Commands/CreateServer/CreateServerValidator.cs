namespace Innovayse.Application.Admin.Servers.Commands.CreateServer;

using FluentValidation;

/// <summary>Validates <see cref="CreateServerCommand"/> before it is handled.</summary>
public sealed class CreateServerValidator : AbstractValidator<CreateServerCommand>
{
    /// <summary>Initialises all validation rules for server creation.</summary>
    public CreateServerValidator()
    {
        RuleFor(x => x.Details.Name)
            .NotEmpty().WithMessage("Server name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Details.Hostname)
            .NotEmpty().WithMessage("Hostname is required.")
            .MaximumLength(253);

        RuleFor(x => x.Details.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(100);

        RuleFor(x => x.Details.MonthlyCost)
            .GreaterThanOrEqualTo(0).WithMessage("Monthly cost cannot be negative.");

        RuleFor(x => x.Details.MaxAccounts)
            .GreaterThan(0).When(x => x.Details.MaxAccounts.HasValue)
            .WithMessage("Max accounts must be greater than zero when specified.");
    }
}
