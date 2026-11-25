using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetRegistrarAutoRenew;

/// <summary>Validates <see cref="SetRegistrarAutoRenewCommand"/> inputs before the handler executes.</summary>
public sealed class SetRegistrarAutoRenewValidator : LocalizedValidator<SetRegistrarAutoRenewCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetRegistrarAutoRenewValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).GreaterThan(0).WithMessage(L["DomainIdRequired"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["AutoRenewValueRequired"]);
    }
}
