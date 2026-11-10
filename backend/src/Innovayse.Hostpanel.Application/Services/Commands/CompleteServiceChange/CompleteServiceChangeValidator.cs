using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;

/// <summary>Validates <see cref="CompleteServiceChangeCommand"/> before the handler executes.</summary>
public sealed class CompleteServiceChangeValidator : LocalizedValidator<CompleteServiceChangeCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CompleteServiceChangeValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.RequestId).MustBePositiveId(L["ServiceChangeRequestIdInvalid"]);
    }
}
