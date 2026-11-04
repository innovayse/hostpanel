using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyCardSave;

/// <summary>Validates <see cref="CompleteMyCardSaveCommand"/> before the handler executes.</summary>
public sealed class CompleteMyCardSaveValidator : LocalizedValidator<CompleteMyCardSaveCommand>
{
    /// <summary>The width of the stored gateway save session id column.</summary>
    private const int SessionIdMaxLength = 255;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CompleteMyCardSaveValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GatewayModuleId).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.SessionId)
            .Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= SessionIdMaxLength)
            .WithMessage(L["SaveSessionIdInvalid"]);
    }
}
