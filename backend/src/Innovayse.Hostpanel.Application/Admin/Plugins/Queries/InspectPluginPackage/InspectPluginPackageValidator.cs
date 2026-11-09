using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.InspectPluginPackage;

/// <summary>
/// Validates <see cref="InspectPluginPackageQuery"/>: only that a file was sent. Whether it is a readable plugin is the
/// handler's answer.
/// </summary>
public sealed class InspectPluginPackageValidator : LocalizedValidator<InspectPluginPackageQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public InspectPluginPackageValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Content).NotNull().WithMessage(L["PluginZipRequired"]);
    }
}
