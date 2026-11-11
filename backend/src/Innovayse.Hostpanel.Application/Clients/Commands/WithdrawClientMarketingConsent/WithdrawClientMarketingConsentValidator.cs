using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.WithdrawClientMarketingConsent;

/// <summary>Validates <see cref="WithdrawClientMarketingConsentCommand"/>: the client id is a positive id.</summary>
public sealed class WithdrawClientMarketingConsentValidator : LocalizedValidator<WithdrawClientMarketingConsentCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public WithdrawClientMarketingConsentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
    }
}
