using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.SetClientExternalAccount;

/// <summary>Validates <see cref="SetClientExternalAccountCommand"/> before the handler executes.</summary>
public sealed class SetClientExternalAccountValidator : LocalizedValidator<SetClientExternalAccountCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetClientExternalAccountValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);

        // The link is whole or absent: both halves present, or both null to clear it.
        RuleFor(x => x).Must(x => (x.Provider is null) == (x.AccountId is null))
            .WithMessage(L["ClientExternalAccountPairRequired"]);

        RuleFor(x => x.Provider!)
            .Must(p => !string.IsNullOrWhiteSpace(p) && p.Trim().Length <= Client.ExternalAccountProviderMaxLength)
            .WithMessage(L["ClientExternalAccountProviderInvalid"])
            .When(x => x.Provider is not null);

        RuleFor(x => x.AccountId!)
            .Must(a => !string.IsNullOrWhiteSpace(a) && a.Trim().Length <= Client.ExternalAccountIdMaxLength)
            .WithMessage(L["ClientExternalAccountIdInvalid"])
            .When(x => x.AccountId is not null);
    }
}
