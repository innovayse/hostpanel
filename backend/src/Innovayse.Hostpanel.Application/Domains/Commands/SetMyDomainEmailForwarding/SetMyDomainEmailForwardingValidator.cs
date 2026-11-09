using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainEmailForwarding;

/// <summary>Validates <see cref="SetMyDomainEmailForwardingCommand"/> before it reaches the handler.</summary>
public sealed class SetMyDomainEmailForwardingValidator : LocalizedValidator<SetMyDomainEmailForwardingCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetMyDomainEmailForwardingValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
