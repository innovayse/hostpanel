using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Commands.DeleteAnnouncement;

/// <summary>Validator for <see cref="DeleteAnnouncementCommand"/>.</summary>
public sealed class DeleteAnnouncementValidator : LocalizedValidator<DeleteAnnouncementCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteAnnouncementValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["AnnouncementIdInvalid"]);
    }
}
