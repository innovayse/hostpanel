using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListDomains;

/// <summary>
/// Validates <see cref="ListDomainsQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the status and registrar filters.
/// </summary>
public sealed class ListDomainsValidator : ListQueryValidator<ListDomainsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    /// <param name="modules">Tells which registrar plugins are installed.</param>
    public ListDomainsValidator(IStringLocalizer<ApplicationValidationMessages> localizer, IRegistrarModuleCatalogue modules)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["DomainStatusInvalid"]);
        RuleFor(x => x.RegistrarModule).MustBeInstalledRegistrar(modules, L["DomainListRegistrarInvalid"]);
    }
}
