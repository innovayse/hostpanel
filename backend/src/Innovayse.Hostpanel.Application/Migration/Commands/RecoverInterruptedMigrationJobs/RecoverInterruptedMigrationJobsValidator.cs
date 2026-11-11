using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Migration.Commands.RecoverInterruptedMigrationJobs;

/// <summary>
///     Validator for RecoverInterruptedMigrationJobsCommand. The command carries no input, so there
///     is nothing to check; the validator exists so every use case has the same shape.
/// </summary>
public sealed class RecoverInterruptedMigrationJobsValidator : LocalizedValidator<RecoverInterruptedMigrationJobsCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecoverInterruptedMigrationJobsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
