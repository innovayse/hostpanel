using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.InviteUserToClient;

/// <summary>Validates <see cref="InviteUserToClientCommand"/> before the handler executes.</summary>
public sealed class InviteUserToClientValidator : LocalizedValidator<InviteUserToClientCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public InviteUserToClientValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256).WithMessage(L["ClientInviteEmailInvalid"]);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage(L["ClientInviteFirstNameInvalid"]);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage(L["ClientInviteLastNameInvalid"]);
        RuleFor(x => x.Permissions)
            .Must(p => (p & ~(int)ClientPermission.All) == 0)
            .WithMessage(L["ClientPermissionsInvalid"]);
    }
}
