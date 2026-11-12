using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Fraud;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.AddBlockListEntry;

/// <summary>Validator for <see cref="AddBlockListEntryCommand"/>: the type is one the product knows, the value fits it, the note fits its column.</summary>
public sealed class AddBlockListEntryValidator : LocalizedValidator<AddBlockListEntryCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AddBlockListEntryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["FraudBlockListTypeInvalid"]);

        // The domain owns what a value of each type looks like; the rule asks it, so the form and the entity never disagree.
        RuleFor(x => x.Value)
            .Must((cmd, value) => BlockListValue.TryNormalise(cmd.Type, value, out _))
            .WithMessage(L["FraudBlockListValueInvalid"]);

        RuleFor(x => x.Note).MaximumLength(BlockListEntry.NoteMaxLength).WithMessage(L["FraudBlockListNoteTooLong"]);
    }
}
