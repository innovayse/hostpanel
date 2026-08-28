namespace Innovayse.Application.Reports.Commands.UpdateDiskUsage;

using FluentValidation;

/// <summary>
/// Validates <see cref="UpdateDiskUsageCommand"/>. The command carries no input, so there is
/// deliberately nothing to check here; the class exists so the use case is a complete set and the
/// next reader knows the absence of rules was decided rather than forgotten.
/// </summary>
public sealed class UpdateDiskUsageValidator : AbstractValidator<UpdateDiskUsageCommand>
{
    /// <summary>Initialises the validator. No rules apply.</summary>
    public UpdateDiskUsageValidator()
    {
    }
}
