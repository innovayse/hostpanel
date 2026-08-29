namespace Innovayse.Application.Support.Commands.CreateNetworkIssue;

using FluentValidation;
using Innovayse.Domain.Support;

/// <summary>Validates <see cref="CreateNetworkIssueCommand"/> before the handler executes.</summary>
/// <remarks>
/// Added because the handler read <c>Type</c> and <c>Priority</c> with <c>Enum.Parse</c> and
/// nothing checked them first, so a bad value reached the caller as a 500 rather than a 400.
/// </remarks>
public sealed class CreateNetworkIssueValidator : AbstractValidator<CreateNetworkIssueCommand>
{
    /// <summary>Initializes validation rules for network issue creation.</summary>
    public CreateNetworkIssueValidator()
    {
        // ignoreCase on both, matching the handler's Enum.Parse<...>(value, true) exactly. A rule
        // stricter than the parse it guards would refuse input the handler accepts; a looser one
        // would approve input the handler throws on.
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<NetworkIssueType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be one of: Server, Other.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(p => Enum.TryParse<NetworkIssuePriority>(p, ignoreCase: true, out _))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.");
    }
}
