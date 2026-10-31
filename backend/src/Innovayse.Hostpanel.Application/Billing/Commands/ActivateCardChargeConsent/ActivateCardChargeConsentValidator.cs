using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ActivateCardChargeConsent;

/// <summary>Validates <see cref="ActivateCardChargeConsentCommand"/> before the handler executes.</summary>
public sealed class ActivateCardChargeConsentValidator : LocalizedValidator<ActivateCardChargeConsentCommand>
{
    /// <summary>The width of the stored gateway id columns.</summary>
    private const int GatewayIdMaxLength = 255;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ActivateCardChargeConsentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.GatewayIntentId)
            .Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= GatewayIdMaxLength)
            .WithMessage(L["SetupIntentIdInvalid"]);
        RuleFor(x => x.PaymentMethodId)
            .Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= GatewayIdMaxLength)
            .WithMessage(L["SetupIntentIdInvalid"]);
    }
}
