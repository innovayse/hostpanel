using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RevokeMyCardConsents;

/// <summary>Validates <see cref="RevokeMyCardConsentsCommand"/>: it carries nothing to check.</summary>
public sealed class RevokeMyCardConsentsValidator : LocalizedValidator<RevokeMyCardConsentsCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RevokeMyCardConsentsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
