namespace Innovayse.Application.Domains.Commands.SetAutoRenew;

using FluentValidation;

/// <summary>Validates <see cref="SetAutoRenewCommand"/> inputs before the handler executes.</summary>
public sealed class SetAutoRenewValidator : AbstractValidator<SetAutoRenewCommand>
{
    /// <summary>Initializes validation rules for the set auto-renew command.</summary>
    public SetAutoRenewValidator()
    {
        RuleFor(x => x.DomainId).GreaterThan(0);
    }
}
