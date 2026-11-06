using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainAutoRenew;

/// <summary>Validates <see cref="SetMyDomainAutoRenewCommand"/> before it reaches the handler.</summary>
public sealed class SetMyDomainAutoRenewValidator : LocalizedValidator<SetMyDomainAutoRenewCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetMyDomainAutoRenewValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["AutoRenewValueRequired"]);
    }
}
