using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeds for the tests of members an identity plugin vouches for: links a client to an external account and
/// signs in a member the stub plugin maps to it.
/// </summary>
public static class PluginMemberSeeds
{
    /// <summary>The system name the stub plugin links its accounts under.</summary>
    public const string Provider = "crm";

    /// <summary>Links an existing client to a fresh external account.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client to link.</param>
    /// <returns>The account id in the plugin system.</returns>
    public static async Task<string> LinkAccountAsync(IntegrationTestFactory factory, int clientId)
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.FirstAsync(c => c.Id == clientId);
        client.SetExternalAccount(Provider, accountId);
        await db.SaveChangesAsync(CancellationToken.None);

        return accountId;
    }

    /// <summary>Registers a user and makes the stub plugin map them to <paramref name="accountId"/>.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="accountId">The account id in the plugin system the member belongs to.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <param name="canManagePaymentMethods">Whether the plugin lets the member manage payment methods.</param>
    /// <returns>The member, to dispose when the test ends.</returns>
    public static async Task<PluginMember> SignInMemberAsync(
        IntegrationTestFactory factory, string label, string accountId, bool canPurchase = false, bool canManagePaymentMethods = false)
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            registered.UserId,
            new ClientAccess(new ExternalAccount(Provider, accountId, null, null), canPurchase, canManagePaymentMethods));
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = registered.Client.DefaultRequestHeaders.Authorization;

        return new PluginMember(host, http, registered.ClientId, registered.Email);
    }
}
