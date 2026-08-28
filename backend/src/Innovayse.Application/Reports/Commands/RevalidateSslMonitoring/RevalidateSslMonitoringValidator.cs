namespace Innovayse.Application.Reports.Commands.RevalidateSslMonitoring;

using FluentValidation;

/// <summary>
/// Validates <see cref="RevalidateSslMonitoringCommand"/>. Its only field is a boolean that is
/// valid either way, so there is deliberately nothing to check; the class exists so the use case
/// is a complete set and the next reader knows this was decided rather than forgotten.
/// </summary>
public sealed class RevalidateSslMonitoringValidator : AbstractValidator<RevalidateSslMonitoringCommand>
{
    /// <summary>Initialises the validator. No rules apply.</summary>
    public RevalidateSslMonitoringValidator()
    {
    }
}
