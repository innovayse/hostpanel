using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CancelStatusMaintenance;

/// <summary>Validates <see cref="CancelStatusMaintenanceCommand"/> before it reaches the handler.</summary>
public sealed class CancelStatusMaintenanceValidator : LocalizedValidator<CancelStatusMaintenanceCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CancelStatusMaintenanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
