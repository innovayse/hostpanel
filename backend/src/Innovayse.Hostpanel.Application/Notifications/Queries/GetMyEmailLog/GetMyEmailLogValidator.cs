using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetMyEmailLog;

/// <summary>Validates <see cref="GetMyEmailLogQuery"/> before the handler executes.</summary>
public sealed class GetMyEmailLogValidator : LocalizedValidator<GetMyEmailLogQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyEmailLogValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.EmailLogId).MustBePositiveId(L["EmailLogIdInvalid"]);
    }
}
