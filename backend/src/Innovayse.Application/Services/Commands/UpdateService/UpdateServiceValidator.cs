namespace Innovayse.Application.Services.Commands.UpdateService;

using FluentValidation;

/// <summary>Validates <see cref="UpdateServiceCommand"/> before the handler executes.</summary>
public sealed class UpdateServiceValidator : AbstractValidator<UpdateServiceCommand>
{
    /// <summary>Initialises validation rules for service update.</summary>
    public UpdateServiceValidator()
    {
        RuleFor(x => x.ServiceId).GreaterThan(0);
        RuleFor(x => x.Domain).MaximumLength(253).When(x => x.Domain is not null);
        RuleFor(x => x.DedicatedIp).MaximumLength(45).When(x => x.DedicatedIp is not null);
        RuleFor(x => x.Username).MaximumLength(100).When(x => x.Username is not null);
        RuleFor(x => x.Password).MaximumLength(256).When(x => x.Password is not null);
        RuleFor(x => x.BillingCycle).NotEmpty().MaximumLength(20);
        RuleFor(x => x.RecurringAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaymentMethod).MaximumLength(50).When(x => x.PaymentMethod is not null);
        RuleFor(x => x.SubscriptionId).MaximumLength(256).When(x => x.SubscriptionId is not null);
        RuleFor(x => x.AutoTerminateReason).MaximumLength(2000).When(x => x.AutoTerminateReason is not null);
        RuleFor(x => x.AdminNotes).MaximumLength(2000).When(x => x.AdminNotes is not null);
        RuleFor(x => x.ProvisioningRef).MaximumLength(256).When(x => x.ProvisioningRef is not null);
        RuleFor(x => x.FirstPaymentAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PromotionCode).MaximumLength(50).When(x => x.PromotionCode is not null);
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<Domain.Services.ServiceStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be one of: Pending, Active, Suspended, Terminated.")
            .When(x => x.Status is not null);
    }
}
