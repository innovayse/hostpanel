using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Queries.ListClients;

/// <summary>
///     Validator for ListClientsQuery. A page or page size out of range is a 400, never a silent
///     correction, so a caller who asks for 5000 rows learns the limit instead of getting 100.
/// </summary>
public sealed class ListClientsValidator : LocalizedValidator<ListClientsQuery>
{
    /// <summary>Longest search, email or phone filter accepted; far above any real name, address or number.</summary>
    private const int MaxFilterLength = 200;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ClientListPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(PagingLimits.MaxPageSize, L["ClientListPageSizeRange"]);

        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a status.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["ClientListStatusInvalid"]);

        RuleFor(x => x.ClientGroupId).MustBePositiveId(L["ContentIdInvalid"]);

        RuleFor(x => x.Search).MaximumLength(MaxFilterLength).WithMessage(L["ClientListSearchTooLong", MaxFilterLength]);
        RuleFor(x => x.Email).MaximumLength(MaxFilterLength).WithMessage(L["ClientListEmailTooLong", MaxFilterLength]);
        RuleFor(x => x.Phone).MaximumLength(MaxFilterLength).WithMessage(L["ClientListPhoneTooLong", MaxFilterLength]);
    }
}
