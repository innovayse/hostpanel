using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceHostingInfo;

/// <summary>Validates <see cref="GetMyServiceHostingInfoQuery"/> before the handler executes.</summary>
public sealed class GetMyServiceHostingInfoValidator : LocalizedValidator<GetMyServiceHostingInfoQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyServiceHostingInfoValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
