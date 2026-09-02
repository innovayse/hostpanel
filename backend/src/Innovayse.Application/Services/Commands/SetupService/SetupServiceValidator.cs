namespace Innovayse.Application.Services.Commands.SetupService;

using System.Text.RegularExpressions;
using FluentValidation;

/// <summary>Validates <see cref="SetupServiceCommand"/> before the service is provisioned.</summary>
/// <remarks>
/// The chokepoint every provisioning path funnels through: the client-facing
/// <c>SetupMyServiceCommand</c> delegates here, and any future staff route dispatches this same
/// command, so the rules cannot be bypassed by choosing a different entry. They mirror the
/// setup wizard's own field checks, because a form check the server does not repeat is not a
/// check at all — a caller reaching the endpoint directly must be refused a malformed domain,
/// a bad username, or a too-short password exactly as the UI would refuse them.
/// </remarks>
public sealed class SetupServiceValidator : AbstractValidator<SetupServiceCommand>
{
    /// <summary>
    /// A hostname of two or more dot-separated labels ending in a letters-only TLD, e.g.
    /// <c>example.com</c>. Each label is a–z/0–9/hyphen without a leading or trailing hyphen;
    /// the whole name is capped at 253 characters. Case-insensitive.
    /// </summary>
    private static readonly Regex DomainPattern = new(
        @"^(?=.{1,253}$)([a-z0-9](-*[a-z0-9])*\.)+[a-z]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Initialises the domain, username, and password rules for service setup.</summary>
    public SetupServiceValidator()
    {
        RuleFor(x => x.Domain)
            .NotEmpty().WithMessage("Domain is required.")
            .Must(d => DomainPattern.IsMatch(d.Trim()))
            .WithMessage("Enter a valid domain, e.g. example.com.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Matches("^[a-z][a-z0-9]{2,7}$")
            .WithMessage("Username must start with a letter, use only lowercase letters and numbers, and be 3 to 8 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}
