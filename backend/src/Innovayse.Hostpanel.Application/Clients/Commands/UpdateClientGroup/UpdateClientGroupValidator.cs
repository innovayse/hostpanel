using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateClientGroup;

/// <summary>Validates <see cref="UpdateClientGroupCommand"/> before the handler executes.</summary>
public sealed class UpdateClientGroupValidator : LocalizedValidator<UpdateClientGroupCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateClientGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.Name).MustBeClientGroupName(L["ClientGroupNameInvalid"]);
        RuleFor(x => x.Description).MustBeClientGroupDescription(L["ClientGroupDescriptionInvalid"]);
        RuleFor(x => x.DiscountPercent).MustBeDiscountPercent(L["ClientGroupDiscountInvalid"]);
        RuleFor(x => x.Color).MustBeClientGroupColor(L["ClientGroupColorInvalid"]);
    }
}
