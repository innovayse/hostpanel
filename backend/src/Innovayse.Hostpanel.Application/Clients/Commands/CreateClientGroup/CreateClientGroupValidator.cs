using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.CreateClientGroup;

/// <summary>Validates <see cref="CreateClientGroupCommand"/> before the handler executes.</summary>
public sealed class CreateClientGroupValidator : LocalizedValidator<CreateClientGroupCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateClientGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Name).MustBeClientGroupName(L["ClientGroupNameInvalid"]);
        RuleFor(x => x.Description).MustBeClientGroupDescription(L["ClientGroupDescriptionInvalid"]);
        RuleFor(x => x.DiscountPercent).MustBeDiscountPercent(L["ClientGroupDiscountInvalid"], L["ClientGroupDiscountDecimalsInvalid"]);
        RuleFor(x => x.Color).MustBeClientGroupColor(L["ClientGroupColorInvalid"]);
    }
}
