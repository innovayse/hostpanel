using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.StartMyTrial;

/// <summary>Validates <see cref="StartMyTrialCommand"/> before the handler executes.</summary>
public sealed class StartMyTrialValidator : LocalizedValidator<StartMyTrialCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public StartMyTrialValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductId).MustBePositiveId(L["StartTrialProductIdInvalid"]);
    }
}
