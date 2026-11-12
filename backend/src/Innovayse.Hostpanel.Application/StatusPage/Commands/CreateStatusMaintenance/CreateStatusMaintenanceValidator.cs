using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusMaintenance;

/// <summary>Validates <see cref="CreateStatusMaintenanceCommand"/> before it reaches the handler.</summary>
public sealed class CreateStatusMaintenanceValidator : LocalizedValidator<CreateStatusMaintenanceCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateStatusMaintenanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(L["StatusMaintenanceTitleRequired"])
            .MaximumLength(StatusMaintenance.TitleMaxLength).WithMessage(L["StatusTitleTooLong", StatusMaintenance.TitleMaxLength]);
        RuleFor(x => x.Description).MaximumLength(StatusMaintenance.DescriptionMaxLength)
            .WithMessage(L["StatusDescriptionTooLong", StatusMaintenance.DescriptionMaxLength]);
        RuleFor(x => x.ScheduledEnd).GreaterThan(x => x.ScheduledStart).WithMessage(L["StatusMaintenanceWindowInvalid"]);
        RuleFor(x => x.ComponentIds).NotEmpty().WithMessage(L["StatusMaintenanceComponentsRequired"]);
        RuleForEach(x => x.ComponentIds).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
