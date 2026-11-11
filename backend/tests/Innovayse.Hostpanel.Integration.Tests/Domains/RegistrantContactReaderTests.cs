using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="RegistrantContactReader"/>: the registrant a registrar order carries is the client's
/// own details, and a detail the client never filled in is left empty for the adapter to refuse.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RegistrantContactReaderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a client, optionally with a phone number and an address.</summary>
    /// <param name="host">The host whose data context the client is saved through.</param>
    /// <param name="withAddress">Whether the client has a phone and an address on record.</param>
    /// <returns>The new client's id.</returns>
    private static async Task<int> SeedClientAsync(WebApplicationFactory<Program> host, bool withAddress)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ani", "Hakobyan", "ignored@example.test", DateTimeOffset.UtcNow);
        if (withAddress)
        {
            client.Update("Ani", "Hakobyan", "Acme LLC", "+374.10123456");
            client.UpdateAddress("1 Main St", "Apt 2", "Yerevan", "Yerevan", "0010", "AM");
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Builds a host whose identity provider answers every subject with the given account.</summary>
    /// <param name="account">The account every subject resolves to, or <see langword="null"/> for none.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostWith(IdentityAccount? account)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IIdentityProvider>(new FakeIdentityProvider(account))));
    }

    /// <summary>The contact is built from the client's name, company, phone and address, and the account's e-mail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReadAsync_BuildsTheContactFromTheClientAsync()
    {
        await using var host = HostWith(new IdentityAccount("sub", "ani@example.test", "Ani", "Hakobyan"));
        var clientId = await SeedClientAsync(host, withAddress: true);
        using var scope = host.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<RegistrantContactReader>();

        var result = await reader.ReadAsync(clientId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var contact = result.Value!;
        contact.FirstName.Should().Be("Ani");
        contact.Organization.Should().Be("Acme LLC");
        contact.Email.Should().Be("ani@example.test");
        contact.Phone.Should().Be("+374.10123456");
        contact.Address1.Should().Be("1 Main St");
        contact.Address2.Should().Be("Apt 2");
        contact.City.Should().Be("Yerevan");
        contact.PostalCode.Should().Be("0010");
        contact.Country.Should().Be("AM");
        contact.IsCompleteForPurchase().Should().BeTrue();
    }

    /// <summary>A client with no address on record gets a contact that is not complete enough to buy with.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReadAsync_WithoutAnAddress_GivesAnIncompleteContactAsync()
    {
        await using var host = HostWith(new IdentityAccount("sub", "ani@example.test", "Ani", "Hakobyan"));
        var clientId = await SeedClientAsync(host, withAddress: false);
        using var scope = host.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<RegistrantContactReader>();

        var result = await reader.ReadAsync(clientId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCompleteForPurchase().Should().BeFalse();
    }

    /// <summary>An unknown client id is refused rather than registered to a blank contact.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReadAsync_WithAnUnknownClient_FailsAsync()
    {
        await using var host = HostWith(null);
        using var scope = host.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<RegistrantContactReader>();

        var result = await reader.ReadAsync(int.MaxValue, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
