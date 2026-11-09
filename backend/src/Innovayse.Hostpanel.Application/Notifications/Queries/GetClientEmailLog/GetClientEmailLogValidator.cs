using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetClientEmailLog;

/// <summary>Validates <see cref="GetClientEmailLogQuery"/>.</summary>
public sealed class GetClientEmailLogValidator : LocalizedValidator<GetClientEmailLogQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetClientEmailLogValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);

        RuleFor(x => x.EmailLogId).MustBePositiveId(L["EmailLogIdInvalid"]);
    }
}
