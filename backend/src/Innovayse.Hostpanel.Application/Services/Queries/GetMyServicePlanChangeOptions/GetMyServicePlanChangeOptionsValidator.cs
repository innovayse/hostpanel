using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServicePlanChangeOptions;

/// <summary>Validates <see cref="GetMyServicePlanChangeOptionsQuery"/> before the handler executes.</summary>
public sealed class GetMyServicePlanChangeOptionsValidator : LocalizedValidator<GetMyServicePlanChangeOptionsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyServicePlanChangeOptionsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
