using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.SetStatusComponentOverride;

/// <summary>Validates <see cref="SetStatusComponentOverrideCommand"/> before it reaches the handler.</summary>
public sealed class SetStatusComponentOverrideValidator : LocalizedValidator<SetStatusComponentOverrideCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetStatusComponentOverrideValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);

        // IsInEnum: the model binder also accepts a numeric value that names no member; a null state clears the override.
        RuleFor(x => x.State).IsInEnum().WithMessage(L["StatusStateInvalid"]);
    }
}
