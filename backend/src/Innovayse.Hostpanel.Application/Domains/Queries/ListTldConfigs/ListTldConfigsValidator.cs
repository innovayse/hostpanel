using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListTldConfigs;

/// <summary>
///     Validates <see cref="ListTldConfigsQuery"/>. The page, page size and search are the shared list rules
///     (<see cref="ListQueryValidator{T}"/>); this adds the rule of the registrar filter.
/// </summary>
public sealed class ListTldConfigsValidator : ListQueryValidator<ListTldConfigsQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    /// <param name="modules">Tells which registrar plugins are installed.</param>
    public ListTldConfigsValidator(IStringLocalizer<ApplicationValidationMessages> localizer, IRegistrarModuleCatalogue modules)
        : base(localizer)
    {
        RuleFor(x => x.RegistrarModule).MustBeInstalledRegistrar(modules, L["DomainListRegistrarInvalid"]);
    }
}
