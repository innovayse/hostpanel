using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListEmailLogs;

/// <summary>
/// Validates <see cref="ListEmailLogsQuery"/>: the page, page size, search and period every list shares
/// (<see cref="PeriodListQueryValidator{T}"/>); the query has no member of its own that needs a rule.
/// </summary>
public sealed class ListEmailLogsValidator : PeriodListQueryValidator<ListEmailLogsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListEmailLogsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
