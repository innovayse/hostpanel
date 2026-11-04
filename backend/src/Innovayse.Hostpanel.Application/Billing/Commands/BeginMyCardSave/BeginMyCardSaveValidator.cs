using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.BeginMyCardSave;

/// <summary>Validates <see cref="BeginMyCardSaveCommand"/> before the handler executes.</summary>
public sealed class BeginMyCardSaveValidator : LocalizedValidator<BeginMyCardSaveCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public BeginMyCardSaveValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GatewayModuleId).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.ReturnUrl).NotEmpty().WithMessage(L["ReturnUrlRequired"])
            .MustBeAbsoluteWebUrl(L["ReturnUrlInvalid"]);
        RuleFor(x => x.ConsentAccepted).Equal(true).WithMessage(L["CardConsentRequired"]);
    }
}
