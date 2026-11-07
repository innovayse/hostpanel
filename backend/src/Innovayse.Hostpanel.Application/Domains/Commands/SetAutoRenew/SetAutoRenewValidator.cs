using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetAutoRenew;

/// <summary>Validates <see cref="SetAutoRenewCommand"/> inputs before the handler executes.</summary>
public sealed class SetAutoRenewValidator : LocalizedValidator<SetAutoRenewCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetAutoRenewValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).GreaterThan(0).WithMessage(L["DomainIdRequired"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["AutoRenewValueRequired"]);
    }
}
