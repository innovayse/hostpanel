using FluentValidation;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.InviteMyUser;

/// <summary>Validates <see cref="InviteMyUserCommand"/> before the handler executes.</summary>
public sealed class InviteMyUserValidator : LocalizedValidator<InviteMyUserCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public InviteMyUserValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256).WithMessage(L["ClientInviteEmailInvalid"]);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage(L["ClientInviteFirstNameInvalid"]);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage(L["ClientInviteLastNameInvalid"]);
        RuleFor(x => x.PermissionMode).IsInEnum().WithMessage(L["ClientPermissionModeInvalid"]);
        RuleFor(x => x.Permissions).NotNull().WithMessage(L["ClientPermissionListInvalid"]);
        RuleFor(x => x.Permissions)
            .Empty()
            .When(x => x.PermissionMode == InvitePermissionMode.All)
            .WithMessage(L["ClientPermissionListMustBeEmpty"]);
        RuleFor(x => x.Permissions)
            .NotEmpty()
            .When(x => x.PermissionMode == InvitePermissionMode.Choose)
            .WithMessage(L["ClientPermissionListRequired"]);
        RuleForEach(x => x.Permissions)
            .Must(IsSinglePermission)
            .WithMessage(L["ClientPermissionListInvalid"]);
    }

    /// <summary>Whether a list entry names exactly one known permission.</summary>
    /// <param name="permission">The list entry.</param>
    /// <returns>True for a single known flag; false for none, a combination or an unknown bit.</returns>
    private static bool IsSinglePermission(ClientPermission permission)
    {
        var bits = (int)permission;
        return bits != 0 && (bits & (bits - 1)) == 0 && (permission & ~ClientPermission.All) == ClientPermission.None;
    }
}
