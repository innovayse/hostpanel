using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListMyEmailLogs;

/// <summary>Validates <see cref="ListMyEmailLogsQuery"/> before it reaches the handler.</summary>
public sealed class ListMyEmailLogsValidator : LocalizedValidator<ListMyEmailLogsQuery>
{
    /// <summary>The largest page size a caller may ask for.</summary>
    private const int MaxPageSize = 100;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListMyEmailLogsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["EmailLogPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["EmailLogPageSizeRange"]);
    }
}
