using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.EnsureCardGatewayCustomer;

/// <summary>Validates <see cref="EnsureCardGatewayCustomerCommand"/> before the handler executes.</summary>
public sealed class EnsureCardGatewayCustomerValidator : LocalizedValidator<EnsureCardGatewayCustomerCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public EnsureCardGatewayCustomerValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
    }
}
