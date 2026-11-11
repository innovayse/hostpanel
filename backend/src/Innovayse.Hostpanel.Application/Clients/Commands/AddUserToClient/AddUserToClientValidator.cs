using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.AddUserToClient;

/// <summary>Validates <see cref="AddUserToClientCommand"/> before the handler executes.</summary>
public sealed class AddUserToClientValidator : LocalizedValidator<AddUserToClientCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AddUserToClientValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.MemberUserId).NotEmpty().WithMessage(L["ClientUserIdRequired"]);
        // Checked here, on the flags value: any bit outside the known set is a
        // malformed request, not a state the handler needs the database to evaluate.
        RuleFor(x => x.Permissions)
            .Must(p => (p & ~ClientPermission.All) == ClientPermission.None)
            .WithMessage(L["ClientPermissionsInvalid"]);
    }
}
