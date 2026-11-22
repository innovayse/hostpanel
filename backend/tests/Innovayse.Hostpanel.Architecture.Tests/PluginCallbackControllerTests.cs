using System.Reflection;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.API.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Pins the shape of the one anonymous plugin door, <see cref="PluginCallbacksController"/>: it is public on purpose, so
/// the three limits that make that safe (the server's body limit, no form binding, its own rate limit) are stated on it, and
/// the signed-in plugin doors stay closed.
/// </summary>
public sealed class PluginCallbackControllerTests
{
    /// <summary>The callback door is the anonymous one.</summary>
    [Fact]
    public void TheCallbackDoor_IsAnonymous()
    {
        typeof(PluginCallbacksController).GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().NotBeNull();
    }

    /// <summary>The callback door sets the server's body limit above the cap, so an unread body of any size cannot be drained.</summary>
    [Fact]
    public void TheCallbackDoor_SetsTheServersBodyLimit()
    {
        typeof(PluginCallbacksController).GetCustomAttribute<PluginBodyLimitAttribute>(inherit: true).Should().NotBeNull();
    }

    /// <summary>The callback door binds nothing from a form, which would read a body past the cap.</summary>
    [Fact]
    public void TheCallbackDoor_BindsNothingFromAForm()
    {
        typeof(PluginCallbacksController).GetCustomAttribute<NoFormBindingAttribute>(inherit: true).Should().NotBeNull();
    }

    /// <summary>The callback door is measured against its own rate limit tier.</summary>
    [Fact]
    public void TheCallbackDoor_HasItsOwnRateLimit()
    {
        typeof(PluginCallbacksController).GetCustomAttribute<PluginCallbackRateLimitAttribute>(inherit: true).Should().NotBeNull();
    }

    /// <summary>The callback door is mounted under its own prefix, never under the signed-in doors' routes.</summary>
    [Fact]
    public void TheCallbackDoor_HasItsOwnPrefix()
    {
        typeof(PluginCallbacksController).GetCustomAttribute<RouteAttribute>(inherit: true)!.Template.Should().Be("api/plugins");
    }

    /// <summary>
    /// The callback door documents every status it can answer with a redirect: the five the SDK allows, and no other 3xx,
    /// because a response type that is not declared is not in the generated contract.
    /// </summary>
    [Fact]
    public void TheCallbackDoor_DeclaresTheFiveRedirectStatuses()
    {
        var declared = typeof(PluginCallbacksController).GetMethod(nameof(PluginCallbacksController.CallAsync))!
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.StatusCode)
            .Where(status => status is >= 300 and <= 399);

        declared.Should().BeEquivalentTo(
            new[]
            {
                StatusCodes.Status301MovedPermanently,
                StatusCodes.Status302Found,
                StatusCodes.Status303SeeOther,
                StatusCodes.Status307TemporaryRedirect,
                StatusCodes.Status308PermanentRedirect,
            });
    }

    /// <summary>No signed-in plugin door is anonymous: only the callback door may be.</summary>
    /// <param name="controller">A signed-in plugin door.</param>
    [Theory]
    [InlineData(typeof(ClientPluginCallsController))]
    [InlineData(typeof(AdminPluginCallsController))]
    public void TheSignedInDoors_AreNotAnonymous(Type controller)
    {
        controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }
}
