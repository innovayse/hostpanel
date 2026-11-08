using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryInstance;

/// <summary>
///     Validates <see cref="GetRegistryInstanceQuery"/>. The query carries no input, so there is no rule to
///     declare; the validator exists so every use case has one, and a rule added later lands here.
/// </summary>
public sealed class GetRegistryInstanceValidator : LocalizedValidator<GetRegistryInstanceQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetRegistryInstanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
