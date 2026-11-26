using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkAllActivityRead;

/// <summary>
///     Validates <see cref="MarkAllActivityReadCommand"/>. The command carries nothing, so there is nothing to refuse;
///     decided, not forgotten.
/// </summary>
public sealed class MarkAllActivityReadValidator : LocalizedValidator<MarkAllActivityReadCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public MarkAllActivityReadValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
