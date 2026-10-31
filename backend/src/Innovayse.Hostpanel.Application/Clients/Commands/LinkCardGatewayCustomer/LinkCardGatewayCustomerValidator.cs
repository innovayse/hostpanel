using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.LinkCardGatewayCustomer;

/// <summary>Validates <see cref="LinkCardGatewayCustomerCommand"/> before the handler executes.</summary>
public sealed class LinkCardGatewayCustomerValidator : LocalizedValidator<LinkCardGatewayCustomerCommand>
{
    /// <summary>The width of the stored customer id column.</summary>
    private const int CustomerIdMaxLength = 50;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public LinkCardGatewayCustomerValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        RuleFor(x => x.CustomerId)
            .Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= CustomerIdMaxLength)
            .WithMessage(L["CardGatewayCustomerIdInvalid"]);
    }
}
