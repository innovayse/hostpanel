using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SendDomainExpiryWarning;

/// <summary>Validates <see cref="SendDomainExpiryWarningCommand"/>.</summary>
public sealed class SendDomainExpiryWarningValidator : LocalizedValidator<SendDomainExpiryWarningCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SendDomainExpiryWarningValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
