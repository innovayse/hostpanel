using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.SetServiceAutoRenew;

/// <summary>Validates <see cref="SetServiceAutoRenewCommand"/> before the handler executes.</summary>
public sealed class SetServiceAutoRenewValidator : LocalizedValidator<SetServiceAutoRenewCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public SetServiceAutoRenewValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
