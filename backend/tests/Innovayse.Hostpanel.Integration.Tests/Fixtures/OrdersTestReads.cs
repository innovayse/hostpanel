using System.Net.Http.Headers;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// What the Orders tests read back from the database, and the few rows they change behind the use cases' backs, so each of
/// them exists once. Every method takes the host's service provider: all hosts built from the shared factory use one database.
/// </summary>
public static class OrdersTestReads
{
    /// <summary>Builds a client carrying the seeded admin's bearer token against a host derived from the shared factory.</summary>
    /// <param name="host">The derived host to send requests to.</param>
    /// <param name="factory">The shared factory that mints the admin token.</param>
    /// <returns>An authenticated admin <see cref="HttpClient"/>.</returns>
    public static async Task<HttpClient> AdminOfAsync(WebApplicationFactory<Program> host, IntegrationTestFactory factory)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }

    /// <summary>Reads an order's lines from the database, with their fulfilment records.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="orderId">The order.</param>
    /// <returns>The lines in id order.</returns>
    public static async Task<List<OrderItem>> ReadItemsAsync(IServiceProvider services, int orderId)
    {
        return (await ReadOrderWithItemsAsync(services, orderId)).Items.OrderBy(i => i.Id).ToList();
    }

    /// <summary>Reads an order with its lines from the database.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="orderId">The order.</param>
    /// <returns>The order and its lines.</returns>
    public static async Task<Order> ReadOrderWithItemsAsync(IServiceProvider services, int orderId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
    }

    /// <summary>Removes the domain another client held, which is what made the registrar refuse the registration.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="domain">The domain name.</param>
    /// <returns>A task that completes once the domain is gone.</returns>
    public static async Task ReleaseDomainAsync(IServiceProvider services, string domain)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = RegisteredDomain.NormaliseName(domain);
        await db.Domains.Where(d => d.Name == name).ExecuteDeleteAsync();
    }

    /// <summary>Counts the domains a client holds under a name.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>How many rows there are.</returns>
    public static async Task<int> CountDomainsAsync(IServiceProvider services, string domain, int clientId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = RegisteredDomain.NormaliseName(domain);
        return await db.Domains.AsNoTracking().CountAsync(d => d.Name == name && d.ClientId == clientId);
    }

    /// <summary>Counts the services a client has of a product.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="productId">The product.</param>
    /// <returns>How many rows there are.</returns>
    public static async Task<int> CountServicesAsync(IServiceProvider services, int clientId, int productId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId && s.ProductId == productId);
    }
}
