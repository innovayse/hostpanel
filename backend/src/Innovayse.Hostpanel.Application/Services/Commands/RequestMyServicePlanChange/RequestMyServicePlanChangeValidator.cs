using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.RequestMyServicePlanChange;

/// <summary>Validates <see cref="RequestMyServicePlanChangeCommand"/> before the handler executes.</summary>
public sealed class RequestMyServicePlanChangeValidator : LocalizedValidator<RequestMyServicePlanChangeCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RequestMyServicePlanChangeValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
        RuleFor(x => x.ToProductId).MustBePositiveId(L["ProductIdInvalid"]);
    }
}
