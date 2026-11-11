using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.PostStatusIncidentUpdate;

/// <summary>Validates <see cref="PostStatusIncidentUpdateCommand"/> before it reaches the handler.</summary>
public sealed class PostStatusIncidentUpdateValidator : LocalizedValidator<PostStatusIncidentUpdateCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public PostStatusIncidentUpdateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);

        // IsInEnum: the model binder also accepts a numeric value that names no member.
        RuleFor(x => x.State).IsInEnum().WithMessage(L["StatusStateInvalid"]);
        RuleFor(x => x.Message).NotEmpty().WithMessage(L["StatusMessageRequired"])
            .MaximumLength(StatusIncidentUpdate.MessageMaxLength).WithMessage(L["StatusMessageTooLong", StatusIncidentUpdate.MessageMaxLength]);
    }
}
