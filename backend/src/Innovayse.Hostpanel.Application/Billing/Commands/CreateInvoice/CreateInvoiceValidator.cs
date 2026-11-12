using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateInvoice;

/// <summary>Validates <see cref="CreateInvoiceCommand"/> before it reaches the handler.</summary>
public sealed class CreateInvoiceValidator : LocalizedValidator<CreateInvoiceCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    /// <param name="clock">The clock today's date is read from.</param>
    public CreateInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer, IClock clock)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["InvoiceClientIdInvalid"]);

        // Compared by date, not by instant: the admin's date picker sends a date-only value that
        // binds to midnight UTC, and "due today" must not count as already in the past.
        RuleFor(x => x.DueDate)
            .Must(d => d.UtcDateTime.Date >= clock.UtcNow.UtcDateTime.Date)
            .WithMessage(L["InvoiceDueDateInPast"]);
        RuleFor(x => x.Items).NotEmpty().WithMessage(L["InvoiceItemsRequired"]);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description)
                .NotEmpty()
                .MaximumLength(InvoiceItem.DescriptionMaxLength)
                .WithMessage(L["InvoiceItemDescriptionInvalid", InvoiceItem.DescriptionMaxLength]);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage(L["InvoiceItemUnitPriceInvalid"]);
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage(L["InvoiceItemQuantityInvalid"]);
        });
    }
}
