using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.DeleteClientGroup;

/// <summary>Validates <see cref="DeleteClientGroupCommand"/> before the handler executes.</summary>
public sealed class DeleteClientGroupValidator : LocalizedValidator<DeleteClientGroupCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteClientGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
