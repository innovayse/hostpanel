using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;

/// <summary>Validates <see cref="AddExistingServiceCommand"/> before the handler executes.</summary>
public sealed class AddExistingServiceValidator : LocalizedValidator<AddExistingServiceCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AddExistingServiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["OrderServiceClientIdInvalid"]);
        RuleFor(x => x.ProductId).MustBePositiveId(L["OrderServiceProductIdInvalid"]);
        RuleFor(x => x.BillingCycle).IsInEnum().WithMessage(L["OrderServiceBillingCycleInvalid"]);
        RuleFor(x => x.RecurringAmount).GreaterThanOrEqualTo(0).WithMessage(L["OrderServiceAmountInvalid"]);
        RuleFor(x => x.ServerId).MustBePositiveId(L["HostingAccountServerIdInvalid"]);
        RuleFor(x => x.Domain).NotEmpty().WithMessage(L["ServiceDomainRequired"])
            .MaximumLength(253).WithMessage(L["HostingAccountDomainTooLong"]);
        RuleFor(x => x.Username).NotEmpty().WithMessage(L["ServiceUsernameRequired"])
            .MaximumLength(100).WithMessage(L["HostingAccountUsernameTooLong"]);
    }
}
