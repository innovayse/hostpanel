using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.RemoveContact;

/// <summary>Validates <see cref="RemoveContactCommand"/> before the handler executes.</summary>
public sealed class RemoveContactValidator : LocalizedValidator<RemoveContactCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RemoveContactValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.ContactId).MustBePositiveId(L["ClientContactIdInvalid"]);
    }
}
