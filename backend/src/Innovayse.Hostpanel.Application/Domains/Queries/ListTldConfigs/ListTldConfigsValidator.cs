using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListTldConfigs;

/// <summary>
///     Validates <see cref="ListTldConfigsQuery"/>. The page, page size and search are the shared list rules
///     (<see cref="ListQueryValidator{T}"/>). The registrar filter is a plugin id and needs no rule of its own: an id no plugin has matches no TLD.
/// </summary>
public sealed class ListTldConfigsValidator : ListQueryValidator<ListTldConfigsQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListTldConfigsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
