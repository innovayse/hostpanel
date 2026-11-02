using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;

/// <summary>Validator for <see cref="SendEmailCommand"/>.</summary>
public sealed class SendEmailValidator : LocalizedValidator<SendEmailCommand>
{
    /// <summary>A SHA-256 in lower-case hex: exactly 64 characters of 0-9 and a-f, anchored at both ends.</summary>
    private const string IdempotencyKeyPattern = @"\A[0-9a-f]{64}\z";

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SendEmailValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress().WithMessage(L["SendEmailToInvalid"]);

        RuleFor(x => x.TemplateSlug).NotEmpty().WithMessage(L["SendEmailTemplateSlugRequired"]);

        // The key is stored in a column sized for a SHA-256 in hex; anything else is a caller bug.
        RuleFor(x => x.IdempotencyKey)
            .Matches(IdempotencyKeyPattern)
            .When(x => x.IdempotencyKey is not null)
            .WithMessage(L["SendEmailIdempotencyKeyInvalid"]);
    }
}
