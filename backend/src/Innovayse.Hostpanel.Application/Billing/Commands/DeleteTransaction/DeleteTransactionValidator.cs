using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DeleteTransaction;

/// <summary>Validates <see cref="DeleteTransactionCommand"/> before it reaches the handler.</summary>
public sealed class DeleteTransactionValidator : LocalizedValidator<DeleteTransactionCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteTransactionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["TransactionIdInvalid"]);
    }
}
