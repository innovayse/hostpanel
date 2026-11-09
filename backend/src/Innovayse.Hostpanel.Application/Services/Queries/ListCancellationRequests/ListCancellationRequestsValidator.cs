using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListCancellationRequests;

/// <summary>Validates <see cref="ListCancellationRequestsQuery"/> before the handler executes.</summary>
public sealed class ListCancellationRequestsValidator : LocalizedValidator<ListCancellationRequestsQuery>
{
    /// <summary>Most rows a page may hold.</summary>
    private const int MaxPageSize = PagingLimits.MaxPageSize;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListCancellationRequestsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ServicePageInvalid"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["ServicePageSizeInvalid"]);
    }
}
