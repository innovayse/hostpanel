using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusMaintenance;

/// <summary>Validates <see cref="ListStatusMaintenanceQuery"/> before it reaches the handler. The query carries no input, so there are no rules.</summary>
public sealed class ListStatusMaintenanceValidator : LocalizedValidator<ListStatusMaintenanceQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListStatusMaintenanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
