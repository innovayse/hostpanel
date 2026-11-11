using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteTldConfig;

/// <summary>
/// Validator for <see cref="DeleteTldConfigCommand"/>. Checks only the shape of the id; whether it
/// names a configured TLD is the handler's answer.
/// </summary>
public sealed class DeleteTldConfigValidator : LocalizedValidator<DeleteTldConfigCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteTldConfigValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["TldConfigIdRequired"]);
    }
}
