using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetCardGatewayCustomer;

/// <summary>Validates <see cref="GetCardGatewayCustomerQuery"/> before the handler executes.</summary>
public sealed class GetCardGatewayCustomerValidator : LocalizedValidator<GetCardGatewayCustomerQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetCardGatewayCustomerValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
    }
}
