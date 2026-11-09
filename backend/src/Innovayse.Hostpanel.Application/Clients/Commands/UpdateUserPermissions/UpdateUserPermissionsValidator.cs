using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateUserPermissions;

/// <summary>Validates <see cref="UpdateUserPermissionsCommand"/> before the handler executes.</summary>
public sealed class UpdateUserPermissionsValidator : LocalizedValidator<UpdateUserPermissionsCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateUserPermissionsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.MemberUserId).NotEmpty().WithMessage(L["ClientUserIdRequired"]);
        RuleFor(x => x.Permissions)
            .Must(p => (p & ~ClientPermission.All) == ClientPermission.None)
            .WithMessage(L["ClientPermissionsInvalid"]);
    }
}
