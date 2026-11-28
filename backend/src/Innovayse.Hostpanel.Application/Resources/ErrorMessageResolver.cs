using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Resources;

/// <summary>
/// <see cref="IErrorMessageResolver"/> over <c>ErrorMessages.resx</c>, in the current request
/// culture. Lives in the Application layer because that is where the resource types are; it does
/// no I/O and reads nothing from the request except the culture the pipeline already set.
/// </summary>
public sealed class ErrorMessageResolver : IErrorMessageResolver
{
    /// <summary>The sentence answered when even the generic resource entry is missing.</summary>
    private const string LastResort = "Something went wrong on our side. Please try again in a moment.";

    /// <summary>The typed localizer the API's <c>AddLocalization()</c> registers.</summary>
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    /// <summary>Initializes a new instance of the <see cref="ErrorMessageResolver"/> class.</summary>
    /// <param name="localizer">The typed localizer the API's <c>AddLocalization()</c> registers.</param>
    public ErrorMessageResolver(IStringLocalizer<ErrorMessages> localizer)
    {
        _localizer = localizer;
    }

    /// <inheritdoc />
    public string Resolve(string code, params object[] arguments)
    {
        var entry = _localizer[code, arguments];
        if (!entry.ResourceNotFound)
        {
            return entry;
        }

        // IStringLocalizer answers the key on a miss rather than throwing. A caller must never see
        // a bare code, so a miss falls back to the generic sentence, and if that is missing too
        // (a broken resource embedding) to a hard-coded one. ResourceCoverageTests makes a miss a
        // build failure long before this runs in production.
        var fallback = _localizer[HostpanelErrorCodes.HOSTPANEL_UNEXPECTED];
        return fallback.ResourceNotFound ? LastResort : fallback;
    }
}
