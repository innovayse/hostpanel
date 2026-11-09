using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListGatewayLogs;

/// <summary>
/// Validates <see cref="ListGatewayLogsQuery"/> paging before the handler executes. The filters are
/// free text, dates and an enum, so any value is accepted - decided, not forgotten.
/// </summary>
public sealed class ListGatewayLogsValidator : LocalizedValidator<ListGatewayLogsQuery>
{
    /// <summary>Largest page size an admin listing may ask for.</summary>
    private const int MaxPageSize = PagingLimits.MaxPageSize;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListGatewayLogsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["GatewayLogPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["GatewayLogPageSizeRange"]);
    }
}
