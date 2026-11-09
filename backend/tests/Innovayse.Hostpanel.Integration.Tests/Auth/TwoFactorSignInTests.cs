using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Auth.Commands.SignIn;
using Innovayse.Hostpanel.Application.Auth.Commands.TwoFactorSignIn;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Models.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <see cref="TwoFactorSignInCommand"/>: the pending token the password step hands out and the
/// one-time code open the session; a token that is missing or unreadable, or a wrong code, is refused
/// with its own code. The HTTP-level refusals (forgery, expiry, replay, attempt limits) are in
/// <c>TwoFactorLoginTests</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TwoFactorSignInTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>A password that satisfies the local password policy.</summary>
    private const string Password = "Password123!";

    /// <summary>The API host and the frozen clock its codes are computed from.</summary>
    private readonly TwoFactorHost _two = new(factory);

    /// <summary>A missing, empty or unreadable pending token is refused with the pending-token code.</summary>
    /// <param name="pendingToken">The token the body carried.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%%%")]
    public async Task UnreadablePendingToken_IsRefusedAsync(string? pendingToken)
    {
        var outcome = await InvokeAsync(new TwoFactorSignInCommand(pendingToken, "000000"));

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>A genuine pending token and the current code open the session.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PendingTokenAndRightCode_OpenTheSessionAsync()
    {
        var (email, secret) = await NewTwoFactorAccountAsync();
        var pending = await PendingTokenAsync(email);

        var outcome = await InvokeAsync(new TwoFactorSignInCommand(pending, _two.CurrentCode(secret)));

        outcome.IsSuccess.Should().BeTrue();
        var session = outcome.Value.Should().BeOfType<SessionIssuedDto>().Subject;
        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.ExpiresIn.Should().BePositive();
    }

    /// <summary>A genuine pending token with a wrong code is refused with the code-mismatch code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PendingTokenAndWrongCode_AreRefusedAsync()
    {
        var (email, secret) = await NewTwoFactorAccountAsync();
        var pending = await PendingTokenAsync(email);
        var wrong = _two.CurrentCode(secret) == "000000" ? "111111" : "000000";

        var outcome = await InvokeAsync(new TwoFactorSignInCommand(pending, wrong));

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.TWO_FACTOR_CODE_UNAUTHORIZED);
    }

    /// <summary>Disposes the clocked host.</summary>
    public void Dispose()
    {
        _two.Dispose();
    }

    /// <summary>Invokes a sign-in command through the clocked host's bus, in a scope of its own.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<ISessionGrant>> InvokeAsync(object command)
    {
        using var scope = _two.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result<ISessionGrant>>(command);
    }

    /// <summary>Signs in with the password through the bus and returns the pending token it hands out.</summary>
    /// <param name="email">The two-factor account's email.</param>
    /// <returns>The pending token.</returns>
    private async Task<string> PendingTokenAsync(string email)
    {
        var outcome = await InvokeAsync(new SignInCommand(email, Password));

        outcome.IsSuccess.Should().BeTrue();
        return outcome.Value.Should().BeOfType<TwoFactorPendingDto>().Subject.PendingToken;
    }

    /// <summary>Registers an account and enables two-factor authentication on it.</summary>
    /// <returns>The account's email and authenticator secret.</returns>
    private async Task<(string Email, string Secret)> NewTwoFactorAccountAsync()
    {
        var client = _two.CreateClient();
        var email = $"twofactor-signin-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Two", lastName = "Factor" }))
            .EnsureSuccessStatusCode();
        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        using var session = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", session.RootElement.GetProperty("accessToken").GetString()!);
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .EnsureSuccessStatusCode();

        return (email, secret);
    }
}
