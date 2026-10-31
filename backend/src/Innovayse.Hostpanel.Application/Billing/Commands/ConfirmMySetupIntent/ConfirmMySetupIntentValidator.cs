using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ConfirmMySetupIntent;

/// <summary>Validates <see cref="ConfirmMySetupIntentCommand"/> before the handler executes.</summary>
public sealed class ConfirmMySetupIntentValidator : LocalizedValidator<ConfirmMySetupIntentCommand>
{
    /// <summary>The width of the stored gateway intent id column.</summary>
    private const int IntentIdMaxLength = 255;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ConfirmMySetupIntentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.SetupIntentId)
            .Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= IntentIdMaxLength)
            .WithMessage(L["SetupIntentIdInvalid"]);
    }
}
