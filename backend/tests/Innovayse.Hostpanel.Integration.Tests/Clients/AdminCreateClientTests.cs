using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Clients.Commands.AdminCreateClient;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>POST /api/clients/admin</c>: creating a client account from the admin panel, either by
/// provisioning a new account or by linking an existing one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AdminCreateClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Linking a client account to an existing, unlinked Identity subject succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinksAnExistingSubject()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "admin-create-happy");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync("/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clientId = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await admin.GetAsync($"/api/clients/{clientId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("lastName").GetString().Should().Be("Client");
    }

    /// <summary>Linking a client account to a subject that already has one answers <c>EXISTING_USER_ALREADY_HAS_CLIENT</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesASubjectThatAlreadyHasAClient()
    {
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "admin-create-dupe");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: userId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EXISTING_USER_ALREADY_HAS_CLIENT);
    }

    /// <summary>Linking a nonexistent existing-user id answers <c>IDENTITY_USER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnUnknownExistingUser()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: ClientsTestSeeds.NoSuchUserId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

    /// <summary>An unconfigured currency is refused as missing, even on the admin-create path.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnUnconfiguredCurrency()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "admin-create-currency");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId, currency: "ZZZ"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CURRENCY_NOT_FOUND);
    }

    /// <summary>Admin-creating a client is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: ClientsTestSeeds.NoSuchUserId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClient()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "admin-create-client-forbidden");

        var response = await client.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: ClientsTestSeeds.NoSuchUserId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A status that names no member of the enum is refused, not quietly created Active.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnUnknownStatus()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "admin-create-status");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId, status: "NotAStatus"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Clients.AnyAsync(c => c.UserId == subject.UserId)).Should().BeFalse();
    }

    /// <summary>A named status is applied: the client is created Suspended.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreatesTheClientInTheNamedStatus()
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(factory, "admin-create-suspended");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync(
            "/api/clients/admin", ClientsTestSeeds.AdminCreateBody(existingUserId: subject.UserId, status: "Suspended"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clientId = await created.Content.ReadFromJsonAsync<int>();
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("status").GetString().Should().Be("Suspended");
    }

    /// <summary>
    /// A refusal that is decided before the account exists leaves no account behind: an unconfigured
    /// currency on a request that would have created a new account creates neither.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARefusalLeavesNoAccountBehind()
    {
        var email = $"admin-create-orphan-{Guid.NewGuid():N}@example.test";
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/clients/admin", NewUserBody(email, currency: "ZZZ"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        (await identity.FindByEmailAsync(email, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Creating a client with a new account grants the account the client role, leaves one activity
    /// line for the acting admin, and the account signs in.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreatesTheAccountItsRoleAndAnAuditLine()
    {
        var email = $"admin-create-new-{Guid.NewGuid():N}@example.test";
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync("/api/clients/admin", NewUserBody(email));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clientId = await created.Content.ReadFromJsonAsync<int>();
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var account = await identity.FindByEmailAsync(email, CancellationToken.None);
        account.Should().NotBeNull();
        (await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
            .HoldsAsync(account!.Subject, Role.Client, CancellationToken.None)).Should().BeTrue();
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Created Client");
    }

    /// <summary>
    /// When saving the client fails after the account was created, the account is removed again and
    /// the failure surfaces: no orphan account is left, and its address can be used again.
    /// </summary>
    /// <remarks>
    /// The handler is run directly, past the validator, with a street longer than its column: the
    /// database refuses the row, which is the only way left to make the save fail once every
    /// request-level check has passed.
    /// </remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AFailedSaveRemovesTheAccountItCreated()
    {
        var email = $"admin-create-compensate-{Guid.NewGuid():N}@example.test";
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<AdminCreateClientHandler>(scope.ServiceProvider);
        var command = new AdminCreateClientCommand(
            CreateNewUser: true, ExistingUserId: null, Email: email, Password: ClientsTestSeeds.TestPassword,
            FirstName: "Comp", LastName: "Ensation", CompanyName: null, Phone: null,
            Address: new PostalAddress(new string('s', 300), null, null, null, null, null),
            Preferences: new ClientPreferencesDto(null, null, null, null, null), Status: null);

        var act = () => handler.HandleAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        (await identity.FindByEmailAsync(email, CancellationToken.None)).Should().BeNull();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Clients.AsNoTracking().AnyAsync(c => c.FirstName == "Comp" && c.LastName == "Ensation")).Should().BeFalse();

        // The account was purged, not marked deleted, so its address is free for the admin to try again.
        var retry = await scope.ServiceProvider.GetRequiredService<IUserProvisioning>()
            .CreateAsync(new CreateUserInput(email, ClientsTestSeeds.TestPassword, "Comp", "Ensation"), CancellationToken.None);
        retry.IsSuccess.Should().BeTrue();
    }

    /// <summary>The body <c>POST /api/clients/admin</c> takes when it creates a new account.</summary>
    /// <param name="email">The new account's address.</param>
    /// <param name="currency">The currency to send, or null to leave it unset.</param>
    /// <returns>The request body.</returns>
    private static object NewUserBody(string email, string? currency = null) => new
    {
        createNewUser = true,
        existingUserId = (string?)null,
        email,
        password = ClientsTestSeeds.TestPassword,
        firstName = "Fresh",
        lastName = "Client",
        companyName = (string?)null,
        phone = (string?)null,
        address = ClientsTestSeeds.EmptyAddress,
        preferences = new
        {
            language = (string?)null,
            currency,
            paymentMethod = (string?)null,
            billingContact = (string?)null,
            adminNotes = (string?)null,
        },
        status = (string?)null,
    };
}
