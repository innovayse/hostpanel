namespace Innovayse.Infrastructure.Tests.Integrations.Cwp7;

using Xunit;

/// <summary>
/// Marks a test that talks to a real CWP7 server, and skips it unless one is configured.
/// </summary>
/// <remarks>
/// The alternative — returning early from the test body — makes the runner report a pass, and a
/// test that passes without asserting anything is worse than no test: it reads as coverage of the
/// very call it did not make. Deciding here, at discovery, means the run says "Skipped" and says
/// why.
/// <para>
/// Set <c>CWP7_LIVE_HOST</c> (scheme, host and API port), <c>CWP7_LIVE_KEY</c> and
/// <c>CWP7_LIVE_USER</c> to run it.
/// </para>
/// </remarks>
public sealed class LiveCwp7FactAttribute : FactAttribute
{
    /// <summary>The environment variables a live run needs, all of them.</summary>
    private static readonly string[] RequiredVariables =
        ["CWP7_LIVE_HOST", "CWP7_LIVE_KEY", "CWP7_LIVE_USER"];

    /// <summary>Initialises the attribute, skipping the test when no server is configured.</summary>
    public LiveCwp7FactAttribute()
    {
        var missing = Array.FindAll(
            RequiredVariables,
            name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

        if (missing.Length > 0)
        {
            Skip = $"No live CWP7 server configured; set {string.Join(", ", missing)} to run this.";
        }
    }
}
