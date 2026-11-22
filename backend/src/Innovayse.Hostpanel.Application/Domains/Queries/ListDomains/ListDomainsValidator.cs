using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListDomains;

/// <summary>
/// Validates <see cref="ListDomainsQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rule of the status filter. The registrar filter is a plugin id and needs none: an id no plugin has matches no domain.
/// </summary>
public sealed class ListDomainsValidator : ListQueryValidator<ListDomainsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListDomainsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["DomainStatusInvalid"]);
    }
}
