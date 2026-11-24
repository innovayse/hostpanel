using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.TerminateTrial;

/// <summary>Validates <see cref="TerminateTrialCommand"/>: the service must be a real id.</summary>
public sealed class TerminateTrialValidator : LocalizedValidator<TerminateTrialCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public TerminateTrialValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
