using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusIncident;

/// <summary>Validates <see cref="DeleteStatusIncidentCommand"/> before it reaches the handler.</summary>
public sealed class DeleteStatusIncidentValidator : LocalizedValidator<DeleteStatusIncidentCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteStatusIncidentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
