namespace Innovayse.Application.Support.Commands.CreateTicket;

using FluentValidation;

/// <summary>Validates <see cref="CreateTicketCommand"/> before it reaches the handler.</summary>
public sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public CreateTicketValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(p => Enum.TryParse<Domain.Support.TicketPriority>(p, ignoreCase: true, out _))
            .WithMessage("'Priority' must be one of: Low, Medium, High.");
    }
}
