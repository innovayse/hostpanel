namespace Innovayse.Application.Domains.Commands.UpdateNameservers;

using FluentValidation;

/// <summary>Validates <see cref="UpdateNameserversCommand"/> inputs before the handler executes.</summary>
public sealed class UpdateNameserversValidator : AbstractValidator<UpdateNameserversCommand>
{
    /// <summary>Initializes validation rules for the update nameservers command.</summary>
    public UpdateNameserversValidator()
    {
        RuleFor(x => x.DomainId).GreaterThan(0);
        RuleFor(x => x.Nameservers).Must(ns => ns != null && ns.Count >= 2)
            .WithMessage("At least 2 nameservers are required.");
    }
}
