using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// Tests for the authorization result handler: a refusal is answered once, with the product's body, and no scheme of
/// the policy is challenged afterwards (a second scheme setting a status on the started response threw and was logged
/// as a business rule violation for an ordinary 401 or 403).
/// </summary>
public sealed class ProblemDetailsAuthorizationMiddlewareResultHandlerTests
{
    /// <summary>The policy the handler is given: three schemes, as the product's own policies name.</summary>
    private static readonly AuthorizationPolicy ThreeSchemePolicy = new AuthorizationPolicyBuilder("Bearer", "LocalJwt", "Cookie")
        .RequireAuthenticatedUser()
        .Build();

    /// <summary>Builds a request whose services record any scheme challenge or forbid.</summary>
    /// <param name="schemes">The recorder the stand-in authentication service writes to.</param>
    /// <returns>The context, with a writable body.</returns>
    private static DefaultHttpContext NewContext(RecordingAuthenticationService schemes)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(schemes)
            .AddSingleton<IStringLocalizer<ValidationMessages>, EchoLocalizer<ValidationMessages>>()
            .BuildServiceProvider();

        return new DefaultHttpContext { RequestServices = services, Response = { Body = new MemoryStream() } };
    }

    /// <summary>A forbidden caller gets one 403 body and no scheme is asked to forbid.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Forbidden_AnswersOnceAndChallengesNoSchemeAsync()
    {
        var schemes = new RecordingAuthenticationService();
        var context = NewContext(schemes);

        await new ProblemDetailsAuthorizationMiddlewareResultHandler()
            .HandleAsync(_ => Task.CompletedTask, context, ThreeSchemePolicy, PolicyAuthorizationResult.Forbid());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Body.Length.Should().BeGreaterThan(0);
        schemes.Calls.Should().BeEmpty();
    }

    /// <summary>An unauthenticated caller gets one 401 body and no scheme is asked to challenge.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Challenged_AnswersOnceAndChallengesNoSchemeAsync()
    {
        var schemes = new RecordingAuthenticationService();
        var context = NewContext(schemes);

        await new ProblemDetailsAuthorizationMiddlewareResultHandler()
            .HandleAsync(_ => Task.CompletedTask, context, ThreeSchemePolicy, PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Length.Should().BeGreaterThan(0);
        schemes.Calls.Should().BeEmpty();
    }

    /// <summary>An authorized caller is passed on to the rest of the pipeline untouched.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Succeeded_CallsTheNextDelegateAsync()
    {
        var context = NewContext(new RecordingAuthenticationService());
        var reachedNext = false;

        await new ProblemDetailsAuthorizationMiddlewareResultHandler().HandleAsync(
            _ =>
            {
                reachedNext = true;
                return Task.CompletedTask;
            },
            context,
            ThreeSchemePolicy,
            PolicyAuthorizationResult.Success());

        reachedNext.Should().BeTrue();
        context.Response.Body.Length.Should().Be(0);
    }

    /// <summary>An authentication service that records which scheme was challenged or forbidden and does nothing else.</summary>
    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        /// <summary>Gets the calls received, as <c>Challenge:scheme</c> or <c>Forbid:scheme</c>.</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc />
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        /// <inheritdoc />
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Calls.Add($"Challenge:{scheme}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Calls.Add($"Forbid:{scheme}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        /// <inheritdoc />
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
