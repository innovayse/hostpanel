using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListClientEmailLogs;

/// <summary>
/// Validates <see cref="ListClientEmailLogsQuery"/>: the client id, and the page, page size, search and period every list
/// shares (<see cref="PeriodListQueryValidator{T}"/>).
/// </summary>
public sealed class ListClientEmailLogsValidator : PeriodListQueryValidator<ListClientEmailLogsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientEmailLogsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
    }
}
