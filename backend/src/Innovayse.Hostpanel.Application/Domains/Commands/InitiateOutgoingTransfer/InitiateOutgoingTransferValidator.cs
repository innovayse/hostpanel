using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.InitiateOutgoingTransfer;

/// <summary>Validates <see cref="InitiateOutgoingTransferCommand"/> inputs before the handler executes.</summary>
public sealed class InitiateOutgoingTransferValidator : LocalizedValidator<InitiateOutgoingTransferCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public InitiateOutgoingTransferValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
