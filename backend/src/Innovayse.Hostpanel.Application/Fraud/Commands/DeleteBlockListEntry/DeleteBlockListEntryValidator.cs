using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.DeleteBlockListEntry;

/// <summary>Validator for <see cref="DeleteBlockListEntryCommand"/>. Checks only the shape of the id; whether it names an entry is the handler's answer.</summary>
public sealed class DeleteBlockListEntryValidator : LocalizedValidator<DeleteBlockListEntryCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteBlockListEntryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["FraudBlockListEntryIdInvalid"]);
    }
}
