using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Pdf.Settings;

/// <summary>Refuses startup when the invoice PDF ceilings are not usable.</summary>
public sealed class InvoicePdfOptionsValidator : IValidateOptions<InvoicePdfOptions>
{
    /// <summary>Checks the options.</summary>
    /// <param name="name">The options instance name (unused; there is one).</param>
    /// <param name="options">The bound options.</param>
    /// <returns>Success, or a failure naming the offending key.</returns>
    public ValidateOptionsResult Validate(string? name, InvoicePdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxLines <= 0)
        {
            return Fail(nameof(InvoicePdfOptions.MaxLines));
        }

        if (options.MaxConcurrentRenders <= 0)
        {
            return Fail(nameof(InvoicePdfOptions.MaxConcurrentRenders));
        }

        if (options.MaxRenderWaitSeconds is <= 0 or > InvoicePdfOptions.MaxRenderWaitSecondsCeiling)
        {
            return ValidateOptionsResult.Fail(
                $"{InvoicePdfOptions.SectionName}:{nameof(InvoicePdfOptions.MaxRenderWaitSeconds)} must be between 1 and {InvoicePdfOptions.MaxRenderWaitSecondsCeiling} " +
                $"(set {InvoicePdfOptions.SectionName}__{nameof(InvoicePdfOptions.MaxRenderWaitSeconds)}).");
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>Builds the failure for one key, naming the environment variable that sets it.</summary>
    /// <param name="key">The offending property name.</param>
    /// <returns>The failure.</returns>
    private static ValidateOptionsResult Fail(string key)
    {
        return ValidateOptionsResult.Fail(
            $"{InvoicePdfOptions.SectionName}:{key} must be greater than zero (set {InvoicePdfOptions.SectionName}__{key}).");
    }
}
