using FluentValidation;
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
    public ListTldConfigsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.RegistrarModule).IsInEnum().WithMessage(L["DomainListRegistrarInvalid"]);
    }
}
