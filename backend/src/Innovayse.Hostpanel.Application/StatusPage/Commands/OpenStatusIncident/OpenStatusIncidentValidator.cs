using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.OpenStatusIncident;

/// <summary>Validates <see cref="OpenStatusIncidentCommand"/> before it reaches the handler.</summary>
public sealed class OpenStatusIncidentValidator : LocalizedValidator<OpenStatusIncidentCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public OpenStatusIncidentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(L["StatusTitleRequired"])
            .MaximumLength(StatusIncident.TitleMaxLength).WithMessage(L["StatusTitleTooLong", StatusIncident.TitleMaxLength]);
        RuleFor(x => x.Message).NotEmpty().WithMessage(L["StatusMessageRequired"])
            .MaximumLength(StatusIncidentUpdate.MessageMaxLength).WithMessage(L["StatusMessageTooLong", StatusIncidentUpdate.MessageMaxLength]);
    }
}
