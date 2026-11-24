using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.UpgradeMyTrial;

/// <summary>Validates <see cref="UpgradeMyTrialCommand"/> before the handler executes.</summary>
public sealed class UpgradeMyTrialValidator : LocalizedValidator<UpgradeMyTrialCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpgradeMyTrialValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
