using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.ProcessTrial;

/// <summary>Validates <see cref="ProcessTrialCommand"/>: the service must be a real id.</summary>
public sealed class ProcessTrialValidator : LocalizedValidator<ProcessTrialCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public ProcessTrialValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
