using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;

/// <summary>
///     Validates <see cref="GetRecentActivityQuery"/>. Every value of <c>Limit</c> is accepted:
///     the handler substitutes its own default for anything outside 1-50, so a caller is never
///     refused over this field; decided, not forgotten.
/// </summary>
public sealed class GetRecentActivityValidator : LocalizedValidator<GetRecentActivityQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetRecentActivityValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
