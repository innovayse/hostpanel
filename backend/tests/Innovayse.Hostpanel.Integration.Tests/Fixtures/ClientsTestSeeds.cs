using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.IntegrationEvents.Contracts;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and authentication helpers shared by every Clients use-case test file, so the seeding
/// logic exists exactly once rather than once per use case.
/// </summary>
public static class ClientsTestSeeds
{
    /// <summary>Shared test password for every registered client user.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>An id no seeded or created client ever takes.</summary>
    public const int NoSuchClientId = 999_999_999;

    /// <summary>An Identity subject no account, seeded or registered, ever takes.</summary>
    public const string NoSuchUserId = "no-such-subject";

    /// <summary>The wire shape of a postal address with every line left empty.</summary>
    public static readonly object EmptyAddress = new
    {
        street = (string?)null,
        address2 = (string?)null,
        city = (string?)null,
        state = (string?)null,
        postCode = (string?)null,
        country = (string?)null,
    };

    /// <summary>The wire shape of the email notification flags with every kind switched on.</summary>
    public static readonly object AllNotificationsOn = new
    {
        general = true,
        invoice = true,
        support = true,
        product = true,
        domain = true,
        affiliate = true,
    };

    /// <summary>Builds a client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The factory whose admin credentials to mint a token from.</param>
    /// <returns>An authenticated admin <see cref="HttpClient"/>.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Seeds a client row directly against the host's database, bypassing HTTP.</summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="firstName">The seeded client's first name.</param>
    /// <param name="lastName">The seeded client's last name.</param>
    /// <returns>The new client's primary key and the Identity subject it is linked to.</returns>
    /// <remarks>
    /// The subject it links to resolves to nobody: a handler that asks the identity provider
    /// about it answers <c>IDENTITY_USER_NOT_FOUND</c>, which is why a refusal that needs a
    /// genuinely resolving subject goes through <see cref="RegisterRealClientAsync"/> instead.
    /// </remarks>
    public static async Task<SeededClient> SeedClientAsync(
        IntegrationTestFactory factory, string firstName = "Ada", string lastName = "Lovelace")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var userId = $"user-{Guid.NewGuid():N}";
        var client = Client.Create(userId, firstName, lastName, $"{firstName.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);
        return new SeededClient(client.Id, userId);
    }

    /// <summary>
    /// Creates a fresh Identity subject straight through <see cref="IUserService"/>, so no
    /// <see cref="ClientRegisteredIntegrationEvent"/> is ever published for it and no client row
    /// ever links to it.
    /// </summary>
    /// <param name="factory">The factory whose host owns the identity store.</param>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <returns>The newly created, still unlinked account.</returns>
    /// <remarks>
    /// <c>/api/auth/register</c> publishes the event that makes <c>CreateClientOnRegisterHandler</c>
    /// link a client row, and that handler runs on a queue: a test that needs an unlinked subject
    /// would race it and sometimes find the subject already linked.
    /// </remarks>
    public static async Task<UnlinkedSubject> CreateUnlinkedSubjectAsync(
        IntegrationTestFactory factory, string label)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        var email = $"{label}-{Guid.NewGuid():N}@example.test";
        var userId = await users.CreateAsync(
            new CreateUserInput(email, TestPassword, "Fresh", "Subject"), CancellationToken.None);
        return new UnlinkedSubject(userId, email);
    }

    /// <summary>
    /// Creates an unlinked Identity subject that holds the client role, the kind of account a client
    /// can be handed to or shared with.
    /// </summary>
    /// <param name="factory">The factory whose host owns the identity store.</param>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <returns>The newly created, still unlinked account, holding the client role.</returns>
    public static async Task<UnlinkedSubject> CreateUnlinkedClientSubjectAsync(
        IntegrationTestFactory factory, string label)
    {
        var subject = await CreateUnlinkedSubjectAsync(factory, label);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
            .AddAsync(subject.UserId, Role.Client, CancellationToken.None);
        return subject;
    }

    /// <summary>Creates an unlinked Identity subject that holds a staff role, never a client one.</summary>
    /// <param name="factory">The factory whose host owns the identity store.</param>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <returns>The newly created account, holding the reseller role.</returns>
    public static async Task<UnlinkedSubject> CreateUnlinkedStaffSubjectAsync(
        IntegrationTestFactory factory, string label)
    {
        var subject = await CreateUnlinkedSubjectAsync(factory, label);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
            .AddAsync(subject.UserId, Role.Reseller, CancellationToken.None);
        return subject;
    }

    /// <summary>
    /// Asserts the client's activity log holds exactly one line that starts with the given words, was
    /// stamped with the acting admin, and carries no personal data (no email address).
    /// </summary>
    /// <param name="factory">The factory whose database to read.</param>
    /// <param name="clientId">The client whose log to read.</param>
    /// <param name="descriptionPrefix">The words the line starts with, for example <c>Transferred Ownership</c>.</param>
    /// <returns>A task that completes once the assertion is made.</returns>
    public static async Task AssertAuditedByAdminAsync(
        IntegrationTestFactory factory, int clientId, string descriptionPrefix)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var lines = await db.ActivityLogs.AsNoTracking().Where(l => l.ClientId == clientId).ToListAsync();

        var line = lines.Should().ContainSingle(l => l.Description.StartsWith(descriptionPrefix, StringComparison.Ordinal)).Subject;
        line.AdminId.Should().NotBeNullOrEmpty("the line is stamped with the admin who acted");
        line.Description.Should().NotContain("@", "the line names the action, not personal data");
    }

    /// <summary>Seeds a client row that has been closed, directly against the host's database.</summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <returns>The closed client's primary key and the subject it is linked to.</returns>
    public static async Task<SeededClient> SeedClosedClientAsync(IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var userId = $"user-{Guid.NewGuid():N}";
        var client = Client.Create(userId, "Closed", "Account", $"closed-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        client.Close(DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);
        return new SeededClient(client.Id, userId);
    }

    /// <summary>
    /// Registers a real Identity account through <c>/api/auth/register</c> and runs
    /// <c>CreateClientOnRegisterHandler</c> published and waited for so the account is linked to a client row.
    /// </summary>
    /// <param name="factory">The factory to register against and publish the event through.</param>
    /// <param name="label">Distinguishes the registered address between callers in the same test.</param>
    /// <returns>The subject and email of the newly registered, now client-linked account.</returns>
    /// <remarks>
    /// <see cref="SeedClientAsync"/> links a client row to a subject nobody registered, which
    /// answers <c>IDENTITY_USER_NOT_FOUND</c> the moment a handler asks the identity provider
    /// about it: this refusal needs a subject that genuinely resolves.
    /// </remarks>
    public static async Task<RegisteredAccount> RegisterRealClientAsync(
        IntegrationTestFactory factory, string label = "linked")
    {
        var http = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.test";
        var registered = await http.PostAsJsonAsync(
            "/api/auth/register", new { email, password = TestPassword, firstName = "Linked", lastName = "Person" });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        using var scope = factory.Services.CreateScope();

        // Registration publishes this event to a local queue that the whole suite shares, so a
        // 10 s poll still timed out under a full run. Publishing a second copy and waiting for its
        // handler makes the row exist now. The handler skips a subject that already has a client, and
        // the unique index on UserId refuses a second row if the queued copy wins the race; the
        // tracked session reports that refusal wrapped in an AggregateException.
        try
        {
            await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(userId, email, "Linked", "Person"));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is DbUpdateException))
        {
            // The queued copy inserted the row between the handler's check and its save.
        }

        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Clients.AnyAsync(c => c.UserId == userId)).Should().BeTrue();
        return new RegisteredAccount(userId, email);
    }

    /// <summary>
    /// Registers a client user over HTTP, logs in, and waits for the Wolverine handler that
    /// creates the corresponding <see cref="Client"/> row, the client-portal equivalent of
    /// <see cref="SeedClientAsync"/>, for use cases resolved from the credential rather than a path id.
    /// </summary>
    /// <param name="factory">The factory to register and mint a token from.</param>
    /// <param name="label">Distinguishes the registered address between callers in the same test.</param>
    /// <returns>The authenticated client, its numeric client id and its Identity subject.</returns>
    public static async Task<SignedInClient> RegisterClientAsync(
        IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"clients-{label}-{Guid.NewGuid():N}@example.test";
        var registered = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Portal",
            lastName = label,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clientId = 0;
        for (var i = 0; i < 10 && clientId == 0; i++)
        {
            await Task.Delay(300);
            var me = await httpClient.GetAsync("/api/clients/me");
            if (me.IsSuccessStatusCode)
            {
                var json = await me.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("id", out var idProp))
                {
                    clientId = idProp.GetInt32();
                }
            }
        }

        clientId.Should().BeGreaterThan(0, "the Wolverine handler should have created the client record");
        return new SignedInClient(httpClient, clientId, userId, email);
    }

    /// <summary>Seeds a pending invitation directly against the host's database.</summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="clientId">The client the invitation is for.</param>
    /// <param name="email">The invitee's email address.</param>
    /// <param name="permissions">The permissions the invitation grants; <c>ViewDomains</c> when not given.</param>
    /// <returns>The invitation's unguessable acceptance token.</returns>
    public static async Task<string> SeedInvitationAsync(
        IntegrationTestFactory factory,
        int clientId,
        string email,
        Domain.Clients.Enums.ClientPermission permissions = Domain.Clients.Enums.ClientPermission.ViewDomains)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var invitation = Invitation.Create(clientId, email, "Invited", "Person", permissions, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(CancellationToken.None);
        return invitation.Token;
    }

    /// <summary>
    /// Creates a signed-in user who owns no client record and is linked to none: the caller of a
    /// linked-user test before any invitation has been accepted.
    /// </summary>
    /// <param name="factory">The factory whose host owns the identity store.</param>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <returns>The user's authenticated client, subject and email.</returns>
    public static async Task<SignedInUnlinkedUser> CreateUnlinkedUserAsync(
        IntegrationTestFactory factory, string label)
    {
        var subject = await CreateUnlinkedSubjectAsync(factory, label);
        var http = factory.CreateClient();
        var token = await factory.GetClientTokenAsync(subject.Email, TestPassword);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new SignedInUnlinkedUser(http, subject.UserId, subject.Email);
    }

    /// <summary>
    /// Links a signed-in user to a client account through the real invitation flow: an invitation
    /// carrying the permissions is accepted over HTTP, which also grants the client role.
    /// </summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="user">The user's authenticated client.</param>
    /// <param name="email">The user's email address, which the invitation names.</param>
    /// <param name="clientId">The account to link to.</param>
    /// <param name="permissions">The permissions the link grants.</param>
    /// <returns>A task that completes once the invitation is accepted.</returns>
    public static async Task LinkUserAsync(
        IntegrationTestFactory factory,
        HttpClient user,
        string email,
        int clientId,
        Domain.Clients.Enums.ClientPermission permissions)
    {
        var token = await SeedInvitationAsync(factory, clientId, email, permissions);
        var accepted = await user.PostAsJsonAsync("/api/auth/accept-invite", new { token });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Creates a user who owns no client record and is linked, with the given permissions, to a freshly
    /// seeded client account: the "other user of the account" the permission checks are about.
    /// </summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <param name="permissions">The permissions the link grants.</param>
    /// <returns>The linked user's authenticated client.</returns>
    public static async Task<HttpClient> LinkedUserAsync(
        IntegrationTestFactory factory, string label, Domain.Clients.Enums.ClientPermission permissions)
    {
        var (user, _, email) = await CreateUnlinkedUserAsync(factory, label);
        var (clientId, _) = await SeedClientAsync(factory);
        await LinkUserAsync(factory, user, email, clientId, permissions);
        return user;
    }

    /// <summary>Creates a user and links it to the given account with the given permissions.</summary>
    /// <param name="factory">The factory whose database to seed against.</param>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <param name="clientId">The account to link to.</param>
    /// <param name="permissions">The permissions the link grants.</param>
    /// <returns>The linked user's authenticated client.</returns>
    public static async Task<HttpClient> LinkedToAsync(
        IntegrationTestFactory factory, string label, int clientId, Domain.Clients.Enums.ClientPermission permissions)
    {
        var (user, _, email) = await CreateUnlinkedUserAsync(factory, label);
        await LinkUserAsync(factory, user, email, clientId, permissions);
        return user;
    }

    /// <summary>Reads the ids of the rows in a list answered as a JSON array.</summary>
    /// <param name="response">The answered list.</param>
    /// <returns>The <c>id</c> of every row.</returns>
    public static async Task<IReadOnlyList<int>> IdsOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetInt32()).ToList();
    }

    /// <summary>Reads the <c>type</c> field of a refusal answered by <c>BaseApiController</c>.</summary>
    /// <param name="response">The refusal.</param>
    /// <returns>The error code.</returns>
    public static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("type").GetString();
    }

    /// <summary>Asserts the refusal carries the expected error code.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected) =>
        (await CodeOfAsync(response)).Should().Be(expected);

    /// <summary>The body <c>PUT /api/clients/{id}</c> takes, every field populated except the override.</summary>
    /// <param name="firstName">The first name to send.</param>
    /// <param name="currency">The currency to send, or null to leave it untouched.</param>
    /// <param name="status">The status string to send, or null to leave it untouched.</param>
    /// <param name="email">The email to send, or null to leave it untouched.</param>
    /// <param name="adminNotes">The internal notes to send, or null to clear them.</param>
    /// <returns>The request body.</returns>
    public static object UpdateBody(
        string firstName = "Ada", string? currency = null, string? status = null, string? email = null, string? adminNotes = null) => new
        {
            email,
            firstName,
            lastName = "Lovelace",
            companyName = (string?)null,
            phone = (string?)null,
            address = EmptyAddress,
            preferences = new
            {
                language = (string?)null,
                currency,
                paymentMethod = (string?)null,
                billingContact = (string?)null,
                adminNotes,
            },
            notifications = AllNotificationsOn,
            settings = new
            {
                overdueNotices = true,
                taxExempt = false,
                separateInvoices = false,
                disableCcProcessing = false,
                marketingOptIn = false,
                statusUpdate = true,
                allowSso = true,
            },
            status,
        };

    /// <summary>The body <c>POST /api/clients/admin</c> takes, linking to an existing Identity subject.</summary>
    /// <param name="existingUserId">The subject id to link.</param>
    /// <param name="currency">The currency to send, or null to leave it unset.</param>
    /// <param name="status">The status string to send, or null to leave it unset.</param>
    /// <returns>The request body.</returns>
    public static object AdminCreateBody(string existingUserId, string? currency = null, string? status = null) => new
    {
        createNewUser = false,
        existingUserId,
        email = (string?)null,
        password = (string?)null,
        firstName = "Second",
        lastName = "Client",
        companyName = (string?)null,
        phone = (string?)null,
        address = EmptyAddress,
        preferences = new
        {
            language = (string?)null,
            currency,
            paymentMethod = (string?)null,
            billingContact = (string?)null,
            adminNotes = (string?)null,
        },
        status,
    };

    /// <summary>A contact body with every field populated.</summary>
    /// <param name="firstName">The first name to send.</param>
    /// <returns>The request body.</returns>
    public static object ContactBody(string firstName) => new
    {
        firstName,
        lastName = "Probe",
        companyName = (string?)null,
        email = "probe@example.test",
        phone = (string?)null,
        type = "General",
        address = EmptyAddress,
        notifications = AllNotificationsOn,
    };

    /// <summary>A contact body that names the address and whether it wants invoice emails; the other kinds stay on.</summary>
    /// <param name="firstName">The first name to send.</param>
    /// <param name="email">The contact's address.</param>
    /// <param name="notifyInvoice">Whether the contact wants invoice emails.</param>
    /// <returns>The request body.</returns>
    public static object ContactBody(string firstName, string email, bool notifyInvoice) => new
    {
        firstName,
        lastName = "Probe",
        companyName = (string?)null,
        email,
        phone = (string?)null,
        type = "General",
        address = EmptyAddress,
        notifications = new
        {
            general = true,
            invoice = notifyInvoice,
            support = true,
            product = true,
            domain = true,
            affiliate = true,
        },
    };
}
