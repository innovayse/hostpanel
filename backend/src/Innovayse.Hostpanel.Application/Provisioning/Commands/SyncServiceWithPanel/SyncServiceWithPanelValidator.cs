using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.SyncServiceWithPanel;

/// <summary>Validates <see cref="SyncServiceWithPanelCommand"/> before the handler executes.</summary>
public sealed class SyncServiceWithPanelValidator : LocalizedValidator<SyncServiceWithPanelCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SyncServiceWithPanelValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
