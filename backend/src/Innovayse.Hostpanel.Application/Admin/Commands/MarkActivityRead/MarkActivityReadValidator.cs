using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Admin;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkActivityRead;

/// <summary>
///     Validates <see cref="MarkActivityReadCommand"/>. The key is stored as it comes, so it must be present and fit the column.
/// </summary>
public sealed class MarkActivityReadValidator : LocalizedValidator<MarkActivityReadCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public MarkActivityReadValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.EventKey)
            .NotEmpty()
            .MaximumLength(ActivityRead.MaxEventKeyLength)
            .WithMessage(L["ActivityEventKeyInvalid"]);
    }
}
