using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetTrialConversionService;

/// <summary>Validates <see cref="GetTrialConversionServiceQuery"/>: the service must be a real id.</summary>
public sealed class GetTrialConversionServiceValidator : LocalizedValidator<GetTrialConversionServiceQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetTrialConversionServiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
