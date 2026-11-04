using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.API.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Every anonymous action that changes state names a rate-limit policy, on itself or its
/// controller. The global limiter alone is sized for page loads, which is far too generous for a
/// write anyone can send without signing in.
/// </summary>
public sealed class AnonymousWriteRateLimitTests
{
    /// <summary>
    /// Controllers whose anonymous writes are checked by a shared key before any handler runs and
    /// are called by one known server, so a per-address budget would only drop legitimate calls.
    /// </summary>
    private static readonly Type[] KeyGatedControllers = [typeof(BackchannelLogoutController)];

    /// <summary>Whether an action is reachable without authentication.</summary>
    /// <param name="action">The controller and action.</param>
    /// <returns><c>true</c> when the action or its controller is <c>[AllowAnonymous]</c>.</returns>
    private static bool IsAnonymous(ControllerAction action)
    {
        return action.Action.IsDefined(typeof(AllowAnonymousAttribute), true)
            || action.Controller.IsDefined(typeof(AllowAnonymousAttribute), true);
    }

    /// <summary>Whether an action answers anything other than a read.</summary>
    /// <param name="action">The controller and action.</param>
    /// <returns><c>true</c> when one of its HTTP methods is not GET or HEAD.</returns>
    private static bool IsWrite(ControllerAction action)
    {
        return action.Action.GetCustomAttributes(typeof(HttpMethodAttribute), true)
            .Cast<HttpMethodAttribute>()
            .SelectMany(attribute => attribute.HttpMethods)
            .Any(method => method is not ("GET" or "HEAD"));
    }

    /// <summary>Whether the action or its controller names a rate-limit policy, or is the plugin callback door, which counts every request against its own tier.</summary>
    /// <param name="action">The controller and action.</param>
    /// <returns><c>true</c> when <c>[EnableRateLimiting]</c> or <c>[PluginCallbackRateLimit]</c> applies to it.</returns>
    private static bool IsRateLimited(ControllerAction action)
    {
        return action.Action.IsDefined(typeof(EnableRateLimitingAttribute), true)
            || action.Controller.IsDefined(typeof(EnableRateLimitingAttribute), true)
            || action.Controller.IsDefined(typeof(PluginCallbackRateLimitAttribute), true);
    }

    /// <summary>The scan finds anonymous writes; a rule over zero actions would pass for the wrong reason.</summary>
    [Fact]
    public void TheScan_FindsAnonymousWrites()
    {
        ControllerAction.All().Where(IsAnonymous).Where(IsWrite).Should().NotBeEmpty();
    }

    /// <summary>Every anonymous write outside the key-gated controllers names a rate-limit policy.</summary>
    [Fact]
    public void EveryAnonymousWrite_NamesARateLimitPolicy()
    {
        var offenders = ControllerAction.All()
            .Where(IsAnonymous)
            .Where(IsWrite)
            .Where(a => !KeyGatedControllers.Contains(a.Controller))
            .Where(a => !IsRateLimited(a))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        offenders.Should().BeEmpty("an anonymous write gets a budget sized for what it does");
    }
}
