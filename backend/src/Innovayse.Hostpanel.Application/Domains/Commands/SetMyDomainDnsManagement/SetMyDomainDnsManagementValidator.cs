using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainDnsManagement;

/// <summary>Validates <see cref="SetMyDomainDnsManagementCommand"/> before it reaches the handler.</summary>
public sealed class SetMyDomainDnsManagementValidator : LocalizedValidator<SetMyDomainDnsManagementCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetMyDomainDnsManagementValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
