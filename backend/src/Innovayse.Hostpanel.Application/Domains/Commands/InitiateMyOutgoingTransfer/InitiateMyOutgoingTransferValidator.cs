using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.InitiateMyOutgoingTransfer;

/// <summary>Validates <see cref="InitiateMyOutgoingTransferCommand"/> before it reaches the handler.</summary>
public sealed class InitiateMyOutgoingTransferValidator : LocalizedValidator<InitiateMyOutgoingTransferCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public InitiateMyOutgoingTransferValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
