using FluentValidation;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServers;

/// <summary>
/// Validates <see cref="ListServersQuery"/> before it reaches the handler. The page, page size and search are the shared
/// list rules (<see cref="ListQueryValidator{T}"/>); this adds the rules of the module and state filters.
/// </summary>
public sealed class ListServersValidator : ListQueryValidator<ListServersQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListServersValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Module).Must(PluginServerModules.IsWellFormed).When(x => x.Module is not null)
            .WithMessage(L["ServerModuleInvalid"]);
        RuleFor(x => x.State).IsInEnum().WithMessage(L["ServerListStateInvalid"]);
    }
}
