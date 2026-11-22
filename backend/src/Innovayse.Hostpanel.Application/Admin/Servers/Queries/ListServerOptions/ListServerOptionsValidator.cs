using FluentValidation;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerOptions;

/// <summary>Validates <see cref="ListServerOptionsQuery"/>: the module filter, when given, names a module.</summary>
public sealed class ListServerOptionsValidator : LocalizedValidator<ListServerOptionsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListServerOptionsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Module).Must(PluginServerModules.IsWellFormed).When(x => x.Module is not null)
            .WithMessage(L["ServerModuleInvalid"]);
    }
}
