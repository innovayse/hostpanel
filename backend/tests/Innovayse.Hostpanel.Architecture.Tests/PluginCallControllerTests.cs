using System.Reflection;
using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.API.Plugins;
using Innovayse.Hostpanel.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The two doors that hand a request to a plugin keep the guards that make them safe: they state the CSRF rule themselves
/// (whatever credential the request carries, so a credential the session middleware does not inspect is still held to
/// it), they set the web server's own body limit to the cap plus one chunk so the configured cap is the one that
/// answers, they bind nothing from a form, and they sit behind their policy.
/// </summary>
public sealed class PluginCallControllerTests
{
    /// <summary>The controllers, with the policy each must be behind.</summary>
    public static TheoryData<Type, string> Doors => new()
    {
        { typeof(ClientPluginCallsController), Policies.Client },
        { typeof(AdminPluginCallsController), Policies.Admin },
    };

    /// <summary>Each door requires the header or a bearer token on a request that changes state.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="policy">The policy it must be behind; unused here.</param>
    [Theory]
    [MemberData(nameof(Doors))]
    public void ThePluginDoors_RequireRequestedWith(Type controller, string policy)
    {
        controller.GetCustomAttribute<RequireRequestedWithAttribute>(inherit: true).Should().NotBeNull(
            $"{controller.Name} hands a request to code the host does not own, so it states the CSRF rule itself ({policy})");
    }

    /// <summary>Each door sets the web server's body limit above the cap, so the configured cap answers an oversize body.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="policy">The policy it must be behind; unused here.</param>
    [Theory]
    [MemberData(nameof(Doors))]
    public void ThePluginDoors_SetTheServersBodyLimitAboveTheCap(Type controller, string policy)
    {
        controller.GetCustomAttribute<PluginBodyLimitAttribute>(inherit: true).Should().NotBeNull(
            $"{controller.Name} reads its body under PluginApi:MaxBodyBytes, which may be above the server's default ({policy})");
    }

    /// <summary>Each door binds nothing from a form, so the runner is the only reader of the request body.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="policy">The policy it must be behind; unused here.</param>
    [Theory]
    [MemberData(nameof(Doors))]
    public void ThePluginDoors_BindNothingFromAForm(Type controller, string policy)
    {
        controller.GetCustomAttribute<NoFormBindingAttribute>(inherit: true).Should().NotBeNull(
            $"{controller.Name} must not let MVC read a form body past PluginApi:MaxBodyBytes ({policy})");
    }

    /// <summary>Each door is behind its own policy.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="policy">The policy it must be behind.</param>
    [Theory]
    [MemberData(nameof(Doors))]
    public void ThePluginDoors_AreBehindTheirPolicy(Type controller, string policy)
    {
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .Should().Contain(policy);
    }
}
