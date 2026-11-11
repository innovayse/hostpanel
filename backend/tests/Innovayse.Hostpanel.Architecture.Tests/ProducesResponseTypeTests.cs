using System.Reflection;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.RateLimiting;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Every controller action documents what it answers: at least one success status, and every
/// failure status it declares carries the one error body, <see cref="ErrorResponse"/>. The
/// generated OpenAPI document is built from these declarations, so an action missing one is a
/// refusal nobody designed. The declarations are also exactly true: no action advertises a status
/// it cannot answer, such as a 401 on an anonymous action or a 429 on an action exempt from rate limiting.
/// </summary>
public sealed class ProducesResponseTypeTests
{
    /// <summary>Every HTTP-bound action of every controller in the API assembly.</summary>
    /// <returns>The controller and action method of each one.</returns>
    private static IReadOnlyList<ControllerAction> AllActions()
    {
        return ControllerAction.All();
    }

    /// <summary>
    /// The response declarations that apply to an action: <c>[ProducesResponseType]</c> and any other
    /// attribute that declares a status, such as a permission filter that documents its own refusal,
    /// on the action and on its controller.
    /// </summary>
    /// <param name="controller">The declaring controller.</param>
    /// <param name="action">The action method.</param>
    /// <returns>The declarations.</returns>
    private static IReadOnlyList<IApiResponseMetadataProvider> DeclarationsOf(Type controller, MethodInfo action)
    {
        return action.GetCustomAttributes(true)
            .Concat(controller.GetCustomAttributes(true))
            .OfType<IApiResponseMetadataProvider>()
            .ToList();
    }

    /// <summary>
    /// The declarations an action inherits from its controller and the controller's bases, without its
    /// own: the statuses the action advertises whether or not it can answer them.
    /// </summary>
    /// <param name="controller">The declaring controller.</param>
    /// <returns>The class-level declarations.</returns>
    private static IReadOnlyList<IApiResponseMetadataProvider> ClassDeclarationsOf(Type controller)
    {
        return controller.GetCustomAttributes(true)
            .OfType<IApiResponseMetadataProvider>()
            .ToList();
    }

    /// <summary>Whether an action is reachable without authentication.</summary>
    /// <param name="action">The controller and action.</param>
    /// <returns><c>true</c> when the action or its controller is <c>[AllowAnonymous]</c>.</returns>
    private static bool IsAnonymous(ControllerAction action)
    {
        return action.Action.IsDefined(typeof(AllowAnonymousAttribute), true)
            || action.Controller.IsDefined(typeof(AllowAnonymousAttribute), true);
    }

    /// <summary>
    /// Whether a status is a success: 2xx, or a redirect that sends the caller on (a tracked link answers 302 to its target).
    /// A 304 is not one: it answers a conditional request with no body and sends the caller nowhere.
    /// </summary>
    /// <param name="status">The HTTP status.</param>
    /// <returns><c>true</c> for 200 to 299 and for the redirects 301, 302, 303, 307 and 308.</returns>
    private static bool IsSuccess(int status)
    {
        return status is >= 200 and < 300 or 301 or 302 or 303 or 307 or 308;
    }

    /// <summary>Whether a status is an error: 4xx or 5xx. A 304 answers with no body at all, so it carries no error body.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <returns><c>true</c> for 400 and above.</returns>
    private static bool IsError(int status)
    {
        return status >= 400;
    }

    /// <summary>The scan finds the controllers; a rule over zero actions would pass for the wrong reason.</summary>
    [Fact]
    public void TheScan_FindsTheActions()
    {
        AllActions().Should().HaveCountGreaterThan(100);
    }

    /// <summary>Every action declares at least one success status: a 2xx, or a redirect that sends the caller on.</summary>
    [Fact]
    public void EveryAction_DeclaresASuccessStatus()
    {
        var offenders = AllActions()
            .Where(a => !DeclarationsOf(a.Controller, a.Action).Any(d => IsSuccess(d.StatusCode)))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        offenders.Should().BeEmpty("every action declares [ProducesResponseType] for the success it answers");
    }

    /// <summary>Every 4xx and 5xx status an action declares carries <see cref="ErrorResponse"/>.</summary>
    [Fact]
    public void EveryFailureStatus_CarriesTheErrorBody()
    {
        var offenders = AllActions()
            .SelectMany(a => DeclarationsOf(a.Controller, a.Action)
                .Where(d => IsError(d.StatusCode) && d.Type != typeof(ErrorResponse))
                .Select(d => $"{a.Controller.Name}.{a.Action.Name} {d.StatusCode}"))
            .ToList();

        offenders.Should().BeEmpty("a failure is always answered with the one ErrorResponse body");
    }

    /// <summary>Every action that is not anonymous declares the 401 its authentication can answer.</summary>
    [Fact]
    public void EveryAuthenticatedAction_DeclaresUnauthorized()
    {
        var offenders = AllActions()
            .Where(a => !IsAnonymous(a))
            .Where(a => !DeclarationsOf(a.Controller, a.Action).Any(d => d.StatusCode == StatusCodes.Status401Unauthorized))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        offenders.Should().BeEmpty("an action behind authentication can answer 401");
    }

    /// <summary>
    /// No anonymous action inherits a 401 from its controller or a base. An anonymous action that
    /// refuses a credential it checks itself, such as a sign-in, declares that 401 on the action.
    /// </summary>
    [Fact]
    public void AnonymousActions_DoNotInheritUnauthorized()
    {
        var offenders = AllActions()
            .Where(IsAnonymous)
            .Where(a => ClassDeclarationsOf(a.Controller).Any(d => d.StatusCode == StatusCodes.Status401Unauthorized))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        offenders.Should().BeEmpty("an anonymous action is never refused by authentication");
    }

    /// <summary>Every action declares the 500 the exception middleware answers for an unhandled fault.</summary>
    [Fact]
    public void EveryAction_DeclaresServerError()
    {
        var offenders = AllActions()
            .Where(a => !DeclarationsOf(a.Controller, a.Action).Any(d => d.StatusCode == StatusCodes.Status500InternalServerError))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        offenders.Should().BeEmpty("every request can reach the exception middleware");
    }

    /// <summary>
    /// Every action the rate limiter measures declares 429, and an action exempt through
    /// <see cref="DisableRateLimitingAttribute"/>, the one place an exemption is declared, does not.
    /// </summary>
    [Fact]
    public void RateLimitedActions_DeclareTooManyRequests_AndExemptActionsDoNot()
    {
        var offenders = AllActions()
            .Where(a => DeclarationsOf(a.Controller, a.Action).Any(d => d.StatusCode == StatusCodes.Status429TooManyRequests)
                == IsRateLimitExempt(a))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        offenders.Should().BeEmpty("429 is declared exactly where the rate limiter can answer it");
    }

    /// <summary>Reports whether the rate limiter skips an action, read from the attribute the limiter honours.</summary>
    /// <param name="action">The controller action to check.</param>
    /// <returns><see langword="true"/> when the action or its controller carries <see cref="DisableRateLimitingAttribute"/>.</returns>
    private static bool IsRateLimitExempt(ControllerAction action)
    {
        return action.Action.GetCustomAttribute<DisableRateLimitingAttribute>(inherit: true) is not null
            || action.Controller.GetCustomAttribute<DisableRateLimitingAttribute>(inherit: true) is not null;
    }
}
