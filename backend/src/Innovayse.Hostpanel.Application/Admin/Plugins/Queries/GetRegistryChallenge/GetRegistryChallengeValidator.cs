using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryChallenge;

/// <summary>
///     Validates <see cref="GetRegistryChallengeQuery"/>. The id is a route value already bound as a GUID, and an id that is
///     not this installation's is answered as not found, so there is no rule to declare; the validator exists so every use
///     case has one, and a rule added later lands here.
/// </summary>
public sealed class GetRegistryChallengeValidator : LocalizedValidator<GetRegistryChallengeQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetRegistryChallengeValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
