using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainWhoisPrivacy;

/// <summary>Validates <see cref="SetMyDomainWhoisPrivacyCommand"/> before it reaches the handler.</summary>
public sealed class SetMyDomainWhoisPrivacyValidator : LocalizedValidator<SetMyDomainWhoisPrivacyCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetMyDomainWhoisPrivacyValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["WhoisPrivacyValueRequired"]);
    }
}
