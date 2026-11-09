using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceHostingAccount;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>PUT /api/services/{id}/hosting-account</c> and <see cref="UpdateServiceHostingAccountHandler"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServiceHostingAccountTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A valid save answers 204 and stores the account details on a service in any status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_OnAnActiveService_StoresTheDetailsAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/services/{serviceId}/hosting-account", new
        {
            domain = "site.example",
            provisioningRef = "acct2",
            dedicatedIp = "198.51.100.7",
            username = "siteuser",
            password = "s3cret-pass",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.ServerId.Should().BeNull();
        stored.Domain.Should().Be("site.example");
        stored.ProvisioningRef.Should().Be("acct2");
        stored.DedicatedIp.Should().Be("198.51.100.7");
        stored.Username.Should().Be("siteuser");
        stored.Password.Should().Be("s3cret-pass");
        stored.Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>A domain longer than a domain can be is refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_WithATooLongDomain_Returns400ValidationErrorAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { domain = new string('a', 254) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Saving the account of a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_OnAnUnknownService_Returns404Async()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/hosting-account", new { domain = "site.example" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>A server id that names no server is refused with 404 <c>SERVER_NOT_FOUND</c> and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_WithAnUnknownServer_Returns404AndKeepsTheServiceAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId = 999_999_999, domain = "site.example" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVER_NOT_FOUND);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.ServerId.Should().BeNull();
        stored.Domain.Should().NotBe("site.example");
    }

    /// <summary>An existing server is accepted and stored on the service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_WithAnExistingServer_StoresTheServerAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "hosting-account.example.test", 22);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId, domain = "site.example" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.ServerId.Should().Be(serverId);
    }

    /// <summary>
    /// On a cPanel server a Pending service takes only a username cPanel would create: a name that
    /// breaks the rule is refused with 409 <c>CPANEL_USERNAME_INVALID</c> and nothing is stored.
    /// </summary>
    /// <param name="username">The username staff entered.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("User")]
    [InlineData("1user")]
    [InlineData("user_name")]
    [InlineData("abcdefghijklmnopq")]
    public async Task UpdateHostingAccount_PendingServiceOnCPanel_WithABadUsername_Returns409Async(string username)
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{Guid.NewGuid():N}.example.test", 22, ServerModule.CPanel);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId, domain = "site.example", username });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CPANEL_USERNAME_INVALID);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.ServerId.Should().BeNull();
        stored.Username.Should().NotBe(username);
    }

    /// <summary>A Pending service on a cPanel server accepts a username cPanel would create.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_PendingServiceOnCPanel_WithAGoodUsername_StoresItAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{Guid.NewGuid():N}.example.test", 22, ServerModule.CPanel);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId, domain = "site.example", username = "siteuser1" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// An account that already exists keeps working under the name it was made with: an Active
    /// service on a cPanel server may carry an older name with an underscore, but never a blank or
    /// separator laden one.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_ActiveServiceOnCPanel_UsesTheLooserRuleForExistingAccountsAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{Guid.NewGuid():N}.example.test", 22, ServerModule.CPanel);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var older = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId, domain = "site.example", username = "Old_User-1" });
        var unsafeName = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId, domain = "site.example", username = "bad name;x" });

        older.StatusCode.Should().Be(HttpStatusCode.NoContent);
        unsafeName.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(unsafeName)).Should().Be(HostpanelErrorCodes.CPANEL_USERNAME_INVALID);
    }

    /// <summary>A panel with no username rule of its own accepts any name staff enter, as before.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_PendingServiceOnAnotherPanel_AcceptsAnyUsernameAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, $"{Guid.NewGuid():N}.example.test", 22, ServerModule.Cwp7);
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/services/{serviceId}/hosting-account", new { serverId, domain = "site.example", username = "User_Name" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Saving without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateHostingAccount_WithoutAuth_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/services/{ServicesTestSeeds.NoSuchServiceId}/hosting-account", new { domain = "site.example" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
