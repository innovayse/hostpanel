using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>api/tickets</c> is staff only: a reseller owns no clients in this product, so it holds no
/// scope to read or change any client's ticket through the staff surface.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StaffTicketAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A reseller can neither read, reply to, close nor list a client's ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResellerCannotReadOrChangeAClientsTicketAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var owner = await SupportTestSeeds.RegisterClientAsync(factory, "staff-ticket-owner");
        var ticketId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin, owner.ClientId);
        var reseller = await ResellerClientAsync();

        var read = await reseller.GetAsync($"/api/tickets/{ticketId}");
        var list = await reseller.GetAsync($"/api/tickets/client/{owner.ClientId}");
        var reply = await reseller.PostAsJsonAsync($"/api/tickets/{ticketId}/reply", new { message = "Not yours." });
        var close = await reseller.PostAsync($"/api/tickets/{ticketId}/close", content: null);

        foreach (var response in new[] { read, list, reply, close })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var ticket = await admin.GetStringAsync($"/api/tickets/{ticketId}");
        ticket.Should().NotContain("Not yours.");
    }

    /// <summary>Creates a signed-in user who holds only the reseller role.</summary>
    /// <returns>The reseller's authenticated client.</returns>
    private async Task<HttpClient> ResellerClientAsync()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "staff-ticket-reseller");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByIdAsync(subject.UserId);
            (await users.AddToRoleAsync(user!, Roles.Reseller)).Succeeded.Should().BeTrue();
        }

        var http = factory.CreateClient();
        var token = await factory.GetClientTokenAsync(subject.Email, ClientsTestSeeds.TestPassword);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }
}
