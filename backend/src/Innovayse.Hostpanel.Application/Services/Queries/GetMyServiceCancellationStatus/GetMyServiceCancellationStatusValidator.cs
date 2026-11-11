using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceCancellationStatus;

/// <summary>Validates <see cref="GetMyServiceCancellationStatusQuery"/> before the handler executes.</summary>
public sealed class GetMyServiceCancellationStatusValidator : LocalizedValidator<GetMyServiceCancellationStatusQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyServiceCancellationStatusValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
