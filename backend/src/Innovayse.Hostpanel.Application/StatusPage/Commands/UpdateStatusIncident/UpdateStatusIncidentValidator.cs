using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusIncident;

/// <summary>Validates <see cref="UpdateStatusIncidentCommand"/> before it reaches the handler.</summary>
public sealed class UpdateStatusIncidentValidator : LocalizedValidator<UpdateStatusIncidentCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateStatusIncidentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.Title).NotEmpty().WithMessage(L["StatusTitleRequired"])
            .MaximumLength(StatusIncident.TitleMaxLength).WithMessage(L["StatusTitleTooLong", StatusIncident.TitleMaxLength]);
    }
}
