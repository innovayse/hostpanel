using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.GetStatusUptime;

/// <summary>Validates <see cref="GetStatusUptimeQuery"/> before it reaches the handler.</summary>
public sealed class GetStatusUptimeValidator : LocalizedValidator<GetStatusUptimeQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetStatusUptimeValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a number that names no window.
        RuleFor(x => x.Days).IsInEnum().WithMessage(L["StatusUptimeWindowInvalid"]);
    }
}
