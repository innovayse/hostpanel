using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;

/// <summary>Validates <see cref="CreateOrderInvoiceCommand"/>.</summary>
public sealed class CreateOrderInvoiceValidator : LocalizedValidator<CreateOrderInvoiceCommand>
{
    /// <summary>Longest line description an invoice stores.</summary>
    private const int DescriptionMaxLength = 500;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateOrderInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["InvoiceClientIdInvalid"]);
        RuleFor(x => x.Currency).MustBeCurrencyCode(L["CurrencyCodeInvalid"]);
        RuleFor(x => x.Items).NotEmpty().WithMessage(L["InvoiceItemsRequired"]);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(DescriptionMaxLength).WithMessage(L["InvoiceItemDescriptionInvalid", DescriptionMaxLength]);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage(L["InvoiceItemUnitPriceInvalid"]);
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage(L["InvoiceItemQuantityInvalid"]);
        });
    }
}
