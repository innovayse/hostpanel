namespace Innovayse.Application.Support.Commands.UpdateTicket;

using FluentValidation;

/// <summary>Validates <see cref="UpdateTicketCommand"/>.</summary>
public sealed class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    /// <summary>Initializes validation rules for ticket updates.</summary>
    public UpdateTicketValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.Status)
            .Must(s => s == null || Enum.TryParse<Innovayse.Domain.Support.TicketStatus>(s, true, out _))
            .WithMessage("Status must be one of: Open, AwaitingReply, Answered, Closed.");
        RuleFor(x => x.Priority)
            .Must(p => p == null || Enum.TryParse<Innovayse.Domain.Support.TicketPriority>(p, true, out _))
            .WithMessage("Priority must be one of: Low, Medium, High.");
    }
}
