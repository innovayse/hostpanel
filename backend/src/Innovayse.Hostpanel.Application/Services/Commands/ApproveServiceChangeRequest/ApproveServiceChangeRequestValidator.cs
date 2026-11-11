using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.ApproveServiceChangeRequest;

/// <summary>Validates <see cref="ApproveServiceChangeRequestCommand"/> before the handler executes.</summary>
public sealed class ApproveServiceChangeRequestValidator : LocalizedValidator<ApproveServiceChangeRequestCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ApproveServiceChangeRequestValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.RequestId).MustBePositiveId(L["ServiceChangeRequestIdInvalid"]);
        RuleFor(x => x.Note).MaximumLength(ServiceChangeRequest.MaxNoteLength)
            .WithMessage(L["ServiceChangeNoteTooLong", ServiceChangeRequest.MaxNoteLength])
            .When(x => x.Note is not null);
    }
}
