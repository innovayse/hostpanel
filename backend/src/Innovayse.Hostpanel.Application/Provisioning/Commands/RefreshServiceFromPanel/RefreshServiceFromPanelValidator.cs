using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.RefreshServiceFromPanel;

/// <summary>Validates <see cref="RefreshServiceFromPanelCommand"/> before the handler executes.</summary>
public sealed class RefreshServiceFromPanelValidator : LocalizedValidator<RefreshServiceFromPanelCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RefreshServiceFromPanelValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
