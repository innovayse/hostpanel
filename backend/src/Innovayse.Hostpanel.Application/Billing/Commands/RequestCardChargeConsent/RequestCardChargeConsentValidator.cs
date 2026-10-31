using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RequestCardChargeConsent;

/// <summary>Validates <see cref="RequestCardChargeConsentCommand"/> before the handler executes.</summary>
public sealed class RequestCardChargeConsentValidator : LocalizedValidator<RequestCardChargeConsentCommand>
{
    /// <summary>The width of the stored gateway intent id column.</summary>
    private const int IntentIdMaxLength = 255;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RequestCardChargeConsentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.GatewayIntentId)
            .Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= IntentIdMaxLength)
            .WithMessage(L["SetupIntentIdInvalid"]);
        RuleFor(x => x.Origin).NotNull().WithMessage(L["CardConsentRequired"]);
    }
}
