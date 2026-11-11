using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DeleteBillableItem;

/// <summary>Validates <see cref="DeleteBillableItemCommand"/> before it reaches the handler.</summary>
public sealed class DeleteBillableItemValidator : LocalizedValidator<DeleteBillableItemCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteBillableItemValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["BillableItemIdInvalid"]);
    }
}
