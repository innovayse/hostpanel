using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListEmailLogs;

/// <summary>
/// Validates <see cref="ListEmailLogsQuery"/> before it reaches the handler. The handler itself
/// clamps page and page size, so this exists only to document that the absence of stricter rules
/// was decided, not overlooked.
/// </summary>
public sealed class ListEmailLogsValidator : LocalizedValidator<ListEmailLogsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListEmailLogsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
