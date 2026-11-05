using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteSaveStoredMethod;

/// <summary>
/// Validator for <see cref="CompleteSaveStoredMethodCommand"/>. Checks only the shape of the ids; whether the session
/// belongs to the caller is the handler's answer.
/// </summary>
public sealed class CompleteSaveStoredMethodValidator : LocalizedValidator<CompleteSaveStoredMethodCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CompleteSaveStoredMethodValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GatewayModuleId).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.SessionId).NotEmpty().WithMessage(L["StoredChargeSessionIdRequired"]);
    }
}
