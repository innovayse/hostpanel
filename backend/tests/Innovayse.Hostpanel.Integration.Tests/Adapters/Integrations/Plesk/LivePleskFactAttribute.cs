namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Plesk;

/// <summary>
/// Marks a test that talks to a real Plesk server, and skips it unless one is configured.
/// </summary>
/// <remarks>
/// Deciding at discovery means the run says "Skipped" and why, instead of a pass that made no call.
/// <para>
/// Set <c>PLESK_LIVE_URL</c> (scheme, host and port, e.g. <c>https://127.0.0.1:8443</c>) and
/// <c>PLESK_LIVE_KEY</c> (a secret key created for the address the tests run from) to run it. The
/// server must have the stock <c>Default Domain</c> and <c>Unlimited</c> service plans and room for
/// one more customer; a trial licence allows exactly one. A test may require more variables, e.g.
/// <c>PLESK_LIVE_CALLER_IP</c> for single sign-on, and is skipped by name while any is missing.
/// </para>
/// </remarks>
public sealed class LivePleskFactAttribute : FactAttribute
{
    /// <summary>The environment variables a live run needs, all of them.</summary>
    private static readonly string[] RequiredVariables = ["PLESK_LIVE_URL", "PLESK_LIVE_KEY"];

    /// <summary>Initialises the attribute, skipping the test when no server, or any extra variable, is configured.</summary>
    /// <param name="extraVariables">Environment variables this test needs beyond the server's address and key.</param>
    public LivePleskFactAttribute(params string[] extraVariables)
    {
        var missing = Array.FindAll(
            [.. RequiredVariables, .. extraVariables],
            name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

        if (missing.Length > 0)
        {
            Skip = $"No live Plesk server configured; set {string.Join(", ", missing)} to run this.";
        }
    }
}
