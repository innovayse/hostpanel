using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteMyDomainEmailForwardingRule;

/// <summary>Validates <see cref="DeleteMyDomainEmailForwardingRuleCommand"/> before it reaches the handler.</summary>
public sealed class DeleteMyDomainEmailForwardingRuleValidator : LocalizedValidator<DeleteMyDomainEmailForwardingRuleCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteMyDomainEmailForwardingRuleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
        RuleFor(x => x.RuleId).MustBePositiveId(L["EmailForwardingRuleIdRequired"]);
    }
}
