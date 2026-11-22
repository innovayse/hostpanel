using FluentValidation;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReadGatewayOrder;

/// <summary>
///     Validator for <see cref="ReadGatewayOrderCommand"/>. Checks only the shape of the plugin id and of the order id; whether
///     an invoice holds such an order is the handler's answer.
/// </summary>
public sealed class ReadGatewayOrderValidator : LocalizedValidator<ReadGatewayOrderCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReadGatewayOrderValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // The shape, not the full plugin id rule, as everywhere a stored plugin is named: an id reserved after the plugin was
        // installed must still be readable.
        RuleFor(x => x.PluginId).Must(PluginIdentifier.IsWellFormed).WithMessage(L["PluginIdInvalid"]);
        RuleFor(x => x.GatewayOrderId).Must(GatewayOrderIds.IsWellFormed).WithMessage(L["GatewayOrderIdInvalid"]);
    }
}
