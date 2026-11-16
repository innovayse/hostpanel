using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Queries.ListClients;

/// <summary>
///     Validator for ListClientsQuery. The page, page size and search are the shared list rules
///     (<see cref="ListQueryValidator{T}"/>); this adds the rules of the client filters.
/// </summary>
public sealed class ListClientsValidator : ListQueryValidator<ListClientsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a status.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["ClientListStatusInvalid"]);

        RuleFor(x => x.ClientGroupId).MustBePositiveId(L["ContentIdInvalid"]);

        RuleFor(x => x.Email).MaximumLength(PagingLimits.MaxSearchLength).WithMessage(L["ClientListEmailTooLong", PagingLimits.MaxSearchLength]);
        RuleFor(x => x.Phone).MaximumLength(PagingLimits.MaxSearchLength).WithMessage(L["ClientListPhoneTooLong", PagingLimits.MaxSearchLength]);
    }
}
