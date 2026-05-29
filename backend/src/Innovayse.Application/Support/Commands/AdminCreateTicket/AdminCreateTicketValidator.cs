namespace Innovayse.Application.Support.Commands.AdminCreateTicket;

using FluentValidation;

/// <summary>Validates <see cref="AdminCreateTicketCommand"/>.</summary>
public sealed class AdminCreateTicketValidator : AbstractValidator<AdminCreateTicketCommand>
{
    /// <summary>Initializes validation rules for admin ticket creation.</summary>
    public AdminCreateTicketValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(p => Enum.TryParse<Innovayse.Domain.Support.TicketPriority>(p, true, out _))
            .WithMessage("Priority must be one of: Low, Medium, High.");
    }
}
