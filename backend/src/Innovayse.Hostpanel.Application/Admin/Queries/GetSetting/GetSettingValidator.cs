using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetSetting;

/// <summary>Validates <see cref="GetSettingQuery"/> before it reaches the handler.</summary>
public sealed class GetSettingValidator : LocalizedValidator<GetSettingQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetSettingValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["SettingIdInvalid"]);
    }
}
