using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Auth.Seeding;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// Proves that under <c>AUTH_MODE=local</c> a role granted through <see cref="ISubjectRoleStore"/>
/// actually authorizes, and that the first-run admin bootstrap can be retried until it completes.
/// </summary>
/// <remarks>
/// <para>
/// In local mode Identity's <c>AspNetUserRoles</c> holds the role granted at registration, while
/// every later grant writes <c>SubjectRoles</c>: <c>AuthController.SetupAsync</c>,
/// <c>AdminCreateClientHandler</c>, <c>PlaceOrderHandler</c>, <c>AcceptInvitationHandler</c> and
/// <c>MigrationPullWorker</c>. If the JWT scheme read only the claims baked into the token, an
/// admin-created client or a guest-checkout customer would get 403 on every
/// <c>[Authorize(Roles = "Client")]</c> route, <c>GET /api/auth/me</c> would report
/// <c>roles: []</c>, and <c>POST /api/auth/setup</c> would grant nothing usable while closing the
/// only bootstrap a standalone install has.
/// </para>
/// <para>
/// The suite runs local mode, the mode these tests guard. Both clear the Admin holders first so
/// neither depends on the order xUnit picks within the class.
/// </para>
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LocalModeSubjectRoleTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A role granted in <c>SubjectRoles</c> after the token was minted authorizes on the very
    /// next request, without a fresh sign-in.
    /// </summary>
    /// <remarks>
    /// The token is deliberately reused across the grant. Re-issuing it would prove only that
    /// sign-in reads the store; the grants that matter happen while the person already holds a
    /// credential.
    /// </remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AGrantInSubjectRolesAuthorizesTheTokenAlreadyIssued()
    {
        var email = $"role-merge-{Guid.NewGuid():N}@innovayse.test";
        const string Password = "Client@Merge123!";

        var subject = await RegisterAsync(email, Password);
        var token = await factory.GetClientTokenAsync(email, Password);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Registration grants Client through Identity, so this token carries Client and nothing
        // else. An Admin-only route has to refuse it, or the test below proves nothing.
        var before = await client.PostAsJsonAsync(
            "/api/products/groups", new { name = $"before-{Guid.NewGuid():N}", description = (string?)null });
        before.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a token carrying only Client must not reach an [Authorize(Roles = \"Admin\")] route");

        await GrantAsync(subject, Role.Admin);

        var after = await client.PostAsJsonAsync(
            "/api/products/groups", new { name = $"after-{Guid.NewGuid():N}", description = (string?)null });
        after.StatusCode.Should().NotBe(
            HttpStatusCode.Forbidden,
            "the role now sits in SubjectRoles, and every JWT scheme merges that store onto the "
            + "principal at validation, which was 403 forever before SubjectRoleClaimsEnricher");
        after.StatusCode.Should().Be(HttpStatusCode.Created);

        // /api/auth/me reports the same store, so the new role is listed there too.
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("roles").EnumerateArray()
            .Select(r => r.GetString())
            .Should().Contain(Roles.Admin);
    }

    /// <summary>
    /// A setup attempt that did not complete leaves the bootstrap open, and one that completes
    /// hands the caller a role that works immediately.
    /// </summary>
    /// <remarks>
    /// <c>setup-required</c> answers <c>AnyHasRoleAsync(Admin)</c>, so the offer closes as soon as
    /// <c>setup</c> writes its row. An attempt that never gets as far as writing must therefore
    /// leave the answer untouched, or a failed attempt would lock the install out for good.
    /// </remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetupIsRetriableUntilItCompletes_AndTheRoleItGrantsWorksAtOnce()
    {
        await ClearAdminHoldersAsync();

        using var anonymous = factory.CreateClient();

        var required = await anonymous.GetFromJsonAsync<JsonElement>("/api/auth/setup-required");
        required.GetProperty("required").GetBoolean().Should().BeTrue(
            "no subject holds Admin, so a fresh standalone install must offer the bootstrap");

        // An unauthenticated attempt. It must refuse without consuming the offer, so the
        // bootstrap can be retried.
        var unauthenticated = await anonymous.PostAsync("/api/auth/setup", content: null);
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var stillRequired = await anonymous.GetFromJsonAsync<JsonElement>("/api/auth/setup-required");
        stillRequired.GetProperty("required").GetBoolean().Should().BeTrue(
            "a setup attempt that granted nothing must leave the bootstrap retriable");

        var email = $"setup-{Guid.NewGuid():N}@innovayse.test";
        const string Password = "Setup@Bootstrap123!";
        await RegisterAsync(email, Password);
        var token = await factory.GetClientTokenAsync(email, Password);

        using var signedIn = factory.CreateClient();
        signedIn.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // The bootstrap demands the token SetupTokenSeeder wrote and the API logged, so a
        // signed-in caller alone is not enough. Without it this call answers 403, which is the
        // point of the token: registering first does not win the race.
        var withoutToken = await signedIn.PostAsJsonAsync(
            "/api/auth/setup", new { setupToken = (string?)null });
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a caller who cannot read the server log must not be able to claim Admin");

        var stillRequiredAfterTokenless = await anonymous.GetFromJsonAsync<JsonElement>(
            "/api/auth/setup-required");
        stillRequiredAfterTokenless.GetProperty("required").GetBoolean().Should().BeTrue(
            "a refused attempt must leave the bootstrap retriable, not spend it");

        var setupToken = await ReadSetupTokenAsync();
        var completed = await signedIn.PostAsJsonAsync(
            "/api/auth/setup", new { setupToken });
        completed.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSetup = await anonymous.GetFromJsonAsync<JsonElement>("/api/auth/setup-required");
        afterSetup.GetProperty("required").GetBoolean().Should().BeFalse(
            "the bootstrap is spent once somebody holds Admin");

        // The grant has to be worth something: a local scheme reads it, so the person who ran
        // setup reaches an admin route.
        var adminRoute = await signedIn.PostAsJsonAsync(
            "/api/products/groups", new { name = $"setup-{Guid.NewGuid():N}", description = (string?)null });
        adminRoute.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "setup must grant a role the caller can actually use, on the credential they already hold");

        // And it cannot be run twice.
        var second = await signedIn.PostAsync("/api/auth/setup", content: null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Registers a local account and returns the subject the token will carry.</summary>
    /// <param name="email">Address to register.</param>
    /// <param name="password">Password for the new account.</param>
    /// <returns>The new account's subject identifier.</returns>
    private async Task<string> RegisterAsync(string email, string password)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password, firstName = "Role", lastName = "Merge" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("userId").GetString()!;
    }

    /// <summary>Grants a role through the store every write path in the product uses.</summary>
    /// <param name="subject">Subject to grant to.</param>
    /// <param name="role">Role to grant.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private async Task GrantAsync(string subject, Role role)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>();
        await store.AddAsync(subject, role, CancellationToken.None);
    }

    /// <summary>
    /// Removes every Admin grant, so the bootstrap test starts from a fresh install regardless of
    /// which order xUnit ran the tests in this class.
    /// </summary>
    /// <returns>A task that completes when the work is done.</returns>
    private async Task ClearAdminHoldersAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.SubjectRoles.Where(x => x.Role == Role.Admin).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Reads the first-run setup token straight from the settings table.
    /// </summary>
    /// <remarks>
    /// The operator gets this from the API's own start-up log. A test cannot read a log line, so
    /// it calls the same seeder the composition root calls at boot: the value one step earlier,
    /// not a hard-coded constant, so the test stays honest about the token being generated per
    /// install.
    ///
    /// <para>
    /// It re-issues rather than reads, because the two halves of "a fresh install" are separate
    /// rows and <see cref="ClearAdminHoldersAsync"/> only restores one. The handler blanks the
    /// token when it is spent, so a second run of this test would read an empty string and see
    /// 403, a green suite on the first run and red on the next. Re-issuing is exactly what a
    /// restart does: the seeder writes a token whenever Admin is unheld, and says nothing once
    /// somebody holds it.
    /// </para>
    /// </remarks>
    /// <returns>The token the seeder wrote on boot.</returns>
    private async Task<string> ReadSetupTokenAsync()
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISetupTokenStore>();
        var roles = scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>();

        var token = await SetupTokenSeeder.EnsureIssuedAsync(store, roles);
        token.Should().NotBeNullOrEmpty(
            "the seeder issues a token whenever Admin is unheld, which ClearAdminHoldersAsync has just made true");
        return token!;
    }
}
