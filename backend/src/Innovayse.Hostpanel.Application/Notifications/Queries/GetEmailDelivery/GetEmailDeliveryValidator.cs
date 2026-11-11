using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailDelivery;

/// <summary>Validates <see cref="GetEmailDeliveryQuery"/>: the key is present.</summary>
public sealed class GetEmailDeliveryValidator : LocalizedValidator<GetEmailDeliveryQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetEmailDeliveryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage(L["EmailDeliveryKeyRequired"]);
    }
}
