using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// The policy table every controller authorizes against: which roles each policy requires, and which
/// schemes it authenticates with in each deployment mode.
/// </summary>
public sealed class PolicyRegistrationTests
{
    /// <summary>Builds the authorization options the way the host does.</summary>
    /// <param name="isLocalMode">Whether the deployment runs in local mode.</param>
    /// <returns>The configured options.</returns>
    private static AuthorizationOptions Build(bool isLocalMode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHostpanelAuthorizationPolicies(isLocalMode);
        return services.BuildServiceProvider().GetRequiredService<IOptions<AuthorizationOptions>>().Value;
    }

    /// <summary>The roles a policy requires; empty when it only requires a signed-in user.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The allowed roles.</returns>
    private static IReadOnlyList<string> RolesOf(AuthorizationPolicy policy)
    {
        return policy.Requirements.OfType<RolesAuthorizationRequirement>().SelectMany(r => r.AllowedRoles).ToList();
    }

    /// <summary>Each role policy requires exactly its roles, and the client portal policy requires the portal requirement instead of a role.</summary>
    /// <param name="isLocalMode">The deployment mode.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Policies_RequireTheirRoles(bool isLocalMode)
    {
        var options = Build(isLocalMode);

        var member = options.GetPolicy(Policies.Member)!;
        RolesOf(member).Should().BeEmpty();
        member.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().ContainSingle();

        RolesOf(options.GetPolicy(Policies.Admin)!).Should().BeEquivalentTo([Roles.Admin]);

        // The client portal is not a plain role check: a member an identity plugin vouches for holds no role.
        var client = options.GetPolicy(Policies.Client)!;
        client.Requirements.OfType<ClientPortalRequirement>().Should().ContainSingle();
        RolesOf(client).Should().BeEmpty();
    }

    /// <summary>In local mode the policies name no scheme: the one default scheme authenticates.</summary>
    [Fact]
    public void LocalMode_PoliciesNameNoScheme()
    {
        var options = Build(isLocalMode: true);

        foreach (var name in new[] { Policies.Member, Policies.Admin, Policies.Client })
        {
            options.GetPolicy(name)!.AuthenticationSchemes.Should().BeEmpty();
        }
    }

    /// <summary>
    /// In the external sign-on mode every policy and the default policy accept the bearer, the local
    /// token and the session cookie, so a role-annotated route reads all three.
    /// </summary>
    [Fact]
    public void SignOnMode_PoliciesAcceptEveryScheme()
    {
        var options = Build(isLocalMode: false);

        foreach (var name in new[] { Policies.Member, Policies.Admin, Policies.Client })
        {
            var schemes = options.GetPolicy(name)!.AuthenticationSchemes;
            schemes.Should().HaveCount(3);
            schemes.Should().Contain([JwtBearerDefaults.AuthenticationScheme, LocalJwtScheme.Name]);
        }

        options.DefaultPolicy.AuthenticationSchemes.Should().HaveCount(3);
    }
}
