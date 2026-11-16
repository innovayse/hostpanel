using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Validates the part every transaction list query shares: the page, page size, search and period
/// (<see cref="PeriodListQueryValidator{T}"/>) and the client, direction, payment method and amount filters
/// (<see cref="ITransactionListFilters"/>). A list derives from this and adds the rules of its own members.
/// </summary>
/// <typeparam name="T">The transaction list query being validated.</typeparam>
public abstract class TransactionListQueryValidator<T> : PeriodListQueryValidator<T>
    where T : IListQuery, ITransactionListFilters
{
    /// <summary>The longest payment method a transaction stores, so a longer filter value can match nothing.</summary>
    private const int PaymentMethodMaxLength = 100;

    /// <summary>Initializes the validator and declares the transaction filter rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    protected TransactionListQueryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ListClientIdInvalid"]);

        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Direction).IsInEnum().WithMessage(L["TransactionDirectionInvalid"]);
        RuleFor(x => x.PaymentMethod).MaximumLength(PaymentMethodMaxLength)
            .WithMessage(L["TransactionPaymentMethodTooLong", PaymentMethodMaxLength]);
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage(L["TransactionAmountInvalid"]);
    }
}
