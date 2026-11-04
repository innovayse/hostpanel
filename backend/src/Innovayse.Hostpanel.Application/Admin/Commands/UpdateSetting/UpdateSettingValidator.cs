using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Commands.UpdateSetting;

/// <summary>
///     Validates <see cref="UpdateSettingCommand"/> before it reaches the handler. <c>Value</c> is
///     only capped here: an empty string is meaningful for some keys and refused for
///     others, and only the handler, which loads the row, knows which. The cap is the one the
///     column is sized for.
/// </summary>
public sealed class UpdateSettingValidator : LocalizedValidator<UpdateSettingCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateSettingValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["SettingIdInvalid"]);
        RuleFor(x => x.Value).MaximumLength(ProtectedValueLimits.SettingValue)
            .WithMessage(L["SettingValueTooLong", ProtectedValueLimits.SettingValue]);
    }
}
