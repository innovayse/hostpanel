using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.DeleteServer;

/// <summary>Validates <see cref="DeleteServerCommand"/> before it reaches the handler.</summary>
public sealed class DeleteServerValidator : LocalizedValidator<DeleteServerCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteServerValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ServerIdInvalid"]);
    }
}
