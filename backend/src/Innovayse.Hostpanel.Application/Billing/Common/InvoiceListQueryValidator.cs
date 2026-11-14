using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Validates the part every invoice list query shares: the page, page size and search (<see cref="ListQueryValidator{T}"/>)
/// and the status and period filters (<see cref="IInvoiceListFilters"/>). A list derives from this and adds the rules of its own members.
/// </summary>
/// <typeparam name="T">The invoice list query being validated.</typeparam>
public abstract class InvoiceListQueryValidator<T> : ListQueryValidator<T>
    where T : IListQuery, IInvoiceListFilters
{
    /// <summary>Initializes the validator and declares the invoice filter rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    protected InvoiceListQueryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["InvoiceStatusInvalid"]);
        RuleFor(x => x.To).MustNotBeBefore(x => x.From, L["DateRangeInvalid"]);
    }
}
