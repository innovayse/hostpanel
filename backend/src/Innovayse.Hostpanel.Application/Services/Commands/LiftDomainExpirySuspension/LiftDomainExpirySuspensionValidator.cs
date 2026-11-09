using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.LiftDomainExpirySuspension;

/// <summary>Validates <see cref="LiftDomainExpirySuspensionCommand"/> before the handler executes.</summary>
public sealed class LiftDomainExpirySuspensionValidator : LocalizedValidator<LiftDomainExpirySuspensionCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public LiftDomainExpirySuspensionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
