using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetSshAgentBundle;

/// <summary>
/// Validates <see cref="GetSshAgentBundleQuery"/>. The profile is an id that is looked up, never trusted, so a bad one is an unknown profile and there is no rule to
/// declare; the validator exists so every query has one, and a rule added later lands here.
/// </summary>
public sealed class GetSshAgentBundleValidator : LocalizedValidator<GetSshAgentBundleQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetSshAgentBundleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
