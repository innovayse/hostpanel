using OtpNet;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The API host of a two-factor test, run on a clock that stands still in the middle of a 30-second
/// TOTP step. Every code the test computes and every code the API checks come from that one clock,
/// so which step is "current" and which is "previous" never depends on when the test happens to run.
/// </summary>
internal sealed class TwoFactorHost : IDisposable
{
    /// <summary>Length of one TOTP time step, in seconds.</summary>
    private const int StepSeconds = 30;

    /// <summary>How far into its step the clock stands; clear of both edges, so the neighbours stay neighbours.</summary>
    private const int OffsetIntoStepSeconds = 15;

    /// <summary>The clocked host the two-factor test runs against.</summary>
    private readonly ClockedHost _host;

    /// <summary>Initializes a new instance of the <see cref="TwoFactorHost"/> class.</summary>
    /// <param name="factory">The shared API + PostgreSQL host the clocked host is derived from.</param>
    public TwoFactorHost(IntegrationTestFactory factory)
    {
        // Real time on purpose: the sign-in tokens the tests mint against the shared host carry real expiry, so the clocked host starts near it.
        var stepStart = TestMoments.Present().ToUnixTimeSeconds() / StepSeconds * StepSeconds;
        _host = new ClockedHost(factory, DateTimeOffset.FromUnixTimeSeconds(stepStart + OffsetIntoStepSeconds));
    }

    /// <summary>Gets the clock both the API and the codes use.</summary>
    public ManualTimeProvider Clock => _host.Clock;

    /// <summary>Gets the services of the clocked host.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>Creates a client for the clocked host.</summary>
    /// <returns>An unauthenticated client.</returns>
    public HttpClient CreateClient()
    {
        return _host.CreateClient();
    }

    /// <summary>Computes the TOTP code of the current step, the way an authenticator app would.</summary>
    /// <param name="secret">The base32-encoded secret.</param>
    /// <returns>The six-digit code of the step the clock stands in.</returns>
    public string CurrentCode(string secret)
    {
        return new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(Clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Computes the code of the step before the current one, which the API still accepts. A code is
    /// good once per account, so a test that enables two-factor with one code and signs in with
    /// another takes this one for the first and <see cref="CurrentCode"/> for the second.
    /// </summary>
    /// <param name="secret">The base32-encoded secret.</param>
    /// <returns>The six-digit code of the previous step.</returns>
    public string PreviousCode(string secret)
    {
        return new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(Clock.GetUtcNow().UtcDateTime.AddSeconds(-StepSeconds));
    }

    /// <summary>Disposes the clocked host.</summary>
    public void Dispose()
    {
        _host.Dispose();
    }
}
