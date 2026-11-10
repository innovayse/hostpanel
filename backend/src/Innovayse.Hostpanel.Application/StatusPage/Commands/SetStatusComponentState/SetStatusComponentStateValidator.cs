using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.SetStatusComponentState;

/// <summary>Validates <see cref="SetStatusComponentStateCommand"/> before it reaches the handler.</summary>
public sealed class SetStatusComponentStateValidator : LocalizedValidator<SetStatusComponentStateCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetStatusComponentStateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);

        // IsInEnum: the model binder also accepts a numeric value that names no member.
        RuleFor(x => x.State).IsInEnum().WithMessage(L["StatusStateInvalid"]);
    }
}
