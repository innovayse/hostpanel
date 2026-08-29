namespace Innovayse.Application.Support.Commands.UpdateNetworkIssue;

using FluentValidation;
using Innovayse.Domain.Support;

/// <summary>Validates <see cref="UpdateNetworkIssueCommand"/> before the handler executes.</summary>
/// <remarks>
/// Added because the handler read <c>Type</c>, <c>Priority</c> and <c>Status</c> with
/// <c>Enum.Parse</c> and nothing checked them first, so a bad value reached the caller as a 500
/// rather than a 400.
/// </remarks>
public sealed class UpdateNetworkIssueValidator : AbstractValidator<UpdateNetworkIssueCommand>
{
    /// <summary>Initializes validation rules for network issue updates.</summary>
    public UpdateNetworkIssueValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        // ignoreCase on all three, matching the handler's Enum.Parse<...>(value, true) exactly.
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<NetworkIssueType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be one of: Server, Other.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(p => Enum.TryParse<NetworkIssuePriority>(p, ignoreCase: true, out _))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<NetworkIssueStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be one of: Reported, Investigating, Scheduled, Resolved.");
    }
}
