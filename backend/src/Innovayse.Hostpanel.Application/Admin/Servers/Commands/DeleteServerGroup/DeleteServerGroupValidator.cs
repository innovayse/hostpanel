using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.DeleteServerGroup;

/// <summary>Validates <see cref="DeleteServerGroupCommand"/> before it reaches the handler.</summary>
public sealed class DeleteServerGroupValidator : LocalizedValidator<DeleteServerGroupCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteServerGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ServerGroupIdInvalid"]);
    }
}
