using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.RecheckVatNumbersCron;

/// <summary>Validates <see cref="RecheckVatNumbersCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class RecheckVatNumbersCronValidator : LocalizedValidator<RecheckVatNumbersCronCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecheckVatNumbersCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
