using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;

/// <summary>Validates <see cref="PlaceOrderCommand"/> before it reaches the handler.</summary>
/// <remarks>
/// Registered Scoped, like every other validator (<c>Program.cs</c>'s
/// <c>AddValidatorsFromAssemblyContaining</c> call): load-bearing here specifically, because this
/// validator injects the Scoped <see cref="ICurrentUserService"/> to decide whether the guest
/// registration fields are required. A Singleton registration would make this validator a captive
/// dependency serving every checkout the first caller's identity.
/// </remarks>
public sealed class PlaceOrderValidator : LocalizedValidator<PlaceOrderCommand>
{
    /// <summary>Longest domain name, the order line column's limit.</summary>
    private const int DomainNameMaxLength = 253;

    /// <summary>Shortest registration period a domain line may ask for, in years.</summary>
    private const int MinYears = 1;

    /// <summary>Longest registration period a domain line may ask for, in years.</summary>
    private const int MaxYears = 10;

    /// <summary>Shortest authorization code a transfer is accepted with, the same as the transfer command's.</summary>
    private const int MinEppCodeLength = 6;

    /// <summary>Initialises all validation rules for placing an order.</summary>
    /// <param name="caller">
    /// Who is checking out. The command names no client, so whether the registration
    /// fields are required is a question about the credential rather than about the message.
    /// </param>
    /// <param name="localizer">String localizer for validation messages.</param>
    public PlaceOrderValidator(ICurrentUserService caller, IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PaymentMethod).NotEmpty().WithMessage(L["PlaceOrderPaymentMethodRequired"]);
        RuleFor(x => x.Items).NotEmpty().WithMessage(L["PlaceOrderItemsRequired"]);

        // The same limit as the client record's phone column the number is stored in.
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage(L["ClientPhoneInvalid"])
            .When(x => x.PhoneNumber is not null);

        // Shape only. Whether the code names a currency this panel offers is an I/O question,
        // and no validator here reads the database; the handler asks it and refuses with the
        // matching error code.
        RuleFor(x => x.Currency)
            .MustBeCurrencyCode(L["CurrencyCodeInvalid"])
            .When(x => x.Currency is not null);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage(L["PlaceOrderItemProductIdInvalid"]);
            item.RuleFor(i => i.BillingCycle).IsInEnum().WithMessage(L["PlaceOrderItemBillingCycleInvalid"]);

            // Domain lines are checked here so the handler never prices or saves a line with an
            // unknown action, no name to price, or a transfer that could never be carried out.
            item.When(i => i.DomainAction is not null, () =>
            {
                item.RuleFor(i => i.DomainAction!.Value).IsInEnum().WithMessage(L["PlaceOrderItemDomainActionInvalid"]);
                item.RuleFor(i => i.Domain)
                    .NotEmpty()
                    .MaximumLength(DomainNameMaxLength)
                    .WithMessage(L["DomainNameRequired"]);
                item.RuleFor(i => i.Years)
                    .InclusiveBetween(MinYears, MaxYears)
                    .When(i => i.Years is not null)
                    .WithMessage(L["DomainYearsRange"]);
                item.RuleFor(i => i.EppCode)
                    .NotEmpty()
                    .MinimumLength(MinEppCodeLength)
                    .When(i => i.DomainAction == DomainAction.Transfer)
                    .WithMessage(L["DomainEppCodeRequired"]);
            });
        });

        // Guest checkout is "no credential", not "no client id in the body". The handler
        // makes the same call, and it has to be the same call: a caller who could decide
        // which branch applied could place an order against an account that is not theirs.
        When(_ => caller.UserId is null, () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().WithMessage(L["PlaceOrderGuestFirstNameRequired"]);
            RuleFor(x => x.LastName).NotEmpty().WithMessage(L["PlaceOrderGuestLastNameRequired"]);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage(L["PlaceOrderGuestEmailRequired"]);
            RuleFor(x => x.Password).NotEmpty().WithMessage(L["PlaceOrderGuestPasswordRequired"]);
        });
    }
}
