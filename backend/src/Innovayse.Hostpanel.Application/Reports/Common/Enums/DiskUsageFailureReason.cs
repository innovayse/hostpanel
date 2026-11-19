namespace Innovayse.Hostpanel.Application.Reports.Common.Enums;

/// <summary>Why a hosting server contributed no figures to a disk usage refresh. Values start at 1.</summary>
public enum DiskUsageFailureReason
{
    /// <summary>The server's control panel cannot report disk and bandwidth use yet; only some control panels can.</summary>
    UnsupportedModule = 1,

    /// <summary>The server has neither an API token nor an access hash to sign in with.</summary>
    MissingCredentials = 2,

    /// <summary>The server did not answer the request, or refused it. The detail is in the log, never in the answer.</summary>
    Unreachable = 3,
}
