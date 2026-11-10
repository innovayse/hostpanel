using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared helpers for the client group tests: the admin route, a valid request body, and creating,
/// reading, defaulting and assigning groups without repeating the HTTP plumbing in every test.
/// </summary>
internal static class ClientGroupTestSeeds
{
    /// <summary>The collection route of the client groups admin API.</summary>
    public const string Route = "/api/admin/client-groups";

    /// <summary>A valid request body for creating or updating a group.</summary>
    /// <param name="name">The name to send, or <see langword="null"/> for a unique one.</param>
    /// <param name="description">The description to send.</param>
    /// <param name="discountPercent">The discount to send.</param>
    /// <param name="color">The colour to send.</param>
    /// <returns>The request body.</returns>
    public static object Body(string? name = null, string? description = null, decimal discountPercent = 10m, string color = "#3B82F6") => new
    {
        name = name ?? $"Group {Guid.NewGuid():N}",
        description,
        discountPercent,
        color,
    };

    /// <summary>Creates a group through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="name">The name to give it, or <see langword="null"/> for a unique one.</param>
    /// <param name="discountPercent">The discount to give it.</param>
    /// <returns>The new group's id.</returns>
    public static Task<int> CreateAsync(HttpClient admin, string? name = null, decimal discountPercent = 10m) =>
        StorefrontContentTestSeeds.CreateAsync(admin, Route, Body(name, discountPercent: discountPercent));

    /// <summary>Reads a group through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="id">The group's id.</param>
    /// <returns>The group as the API answers it.</returns>
    public static async Task<JsonElement> GetAsync(HttpClient admin, int id) =>
        await admin.GetFromJsonAsync<JsonElement>($"{Route}/{id}");

    /// <summary>Makes a group the default, or stops it being the default, through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="id">The group's id.</param>
    /// <param name="isDefault">Whether it is to be the default.</param>
    /// <returns>A task that completes once the API has answered.</returns>
    public static async Task SetDefaultAsync(HttpClient admin, int id, bool isDefault)
    {
        var response = await admin.PutAsJsonAsync($"{Route}/{id}/default", new { isDefault });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Puts an existing client in a group directly against the host's database.</summary>
    /// <param name="factory">The factory whose database to write to.</param>
    /// <param name="clientId">The client to assign.</param>
    /// <param name="groupId">The group to put it in.</param>
    /// <returns>A task that completes once the client is saved.</returns>
    public static async Task AssignAsync(IntegrationTestFactory factory, int clientId, int groupId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.FindAsync(clientId);
        client!.AssignToGroup(groupId);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
