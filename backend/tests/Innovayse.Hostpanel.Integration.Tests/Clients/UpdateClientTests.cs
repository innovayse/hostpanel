using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation.TestHelper;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>PUT /api/clients/{id}</c>: an admin edits a client's profile, address, preferences,
/// currency and status.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating a client's profile writes the change back.</summary>
    [Fact]
    public async Task UpdateWritesTheProfile()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Katherine"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("firstName").GetString().Should().Be("Katherine");
    }

    /// <summary>
    /// An admin can still move a client's sign-in to a new address at once, and doing so ends every
    /// session the client held: the client's earlier token is refused and the new address signs in.
    /// </summary>
    [Fact]
    public async Task UpdateEmailMovesTheSignInAndEndsTheClientSessions()
    {
        var (client, clientId, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "admin-email-change");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var newEmail = $"admin-moved-{Guid.NewGuid():N}@example.test";

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Moved", email: newEmail));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/clients/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = newEmail, password = ClientsTestSeeds.TestPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Updating an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UpdateUnknownClientRefusesNotFound()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}", ClientsTestSeeds.UpdateBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>
    /// Moving an invoiced client to a different currency is refused with
    /// <c>CLIENT_CURRENCY_ALREADY_INVOICED</c>, but the same save with the currency left alone
    /// still writes the other fields.
    /// </summary>
    [Fact]
    public async Task UpdateRefusesACurrencyChangeOnceInvoiced()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Invoices.Add(Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(14), "USD", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var changed = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(currency: "AMD"));
        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(changed, HostpanelErrorCodes.CLIENT_CURRENCY_ALREADY_INVOICED);

        var unchanged = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Margaret"));
        unchanged.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Invoices that were all cancelled do not lock the currency: a client whose only invoice was
    /// cancelled can still be moved to another currency.
    /// </summary>
    [Fact]
    public async Task UpdateAllowsACurrencyChangeWhenEveryInvoiceIsCancelled()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "AMD", "051", 0.0026m);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(14), "USD", DateTimeOffset.UtcNow);
            invoice.Cancel();
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(currency: "AMD"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("preferences").GetProperty("currency").GetString().Should().Be("AMD");
    }

    /// <summary>A currency this panel has not configured and enabled is refused as missing.</summary>
    [Fact]
    public async Task UpdateRefusesAnUnconfiguredCurrency()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(currency: "ZZZ"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CURRENCY_NOT_FOUND);
    }

    /// <summary>A status word that names no member of the enum is refused with 400 before the handler runs.</summary>
    /// <remarks>
    /// The status is bound as the enum, so a word outside it fails binding and never reaches the
    /// handler; a number outside it is refused by the validator (see <see cref="UndefinedStatus_ShouldFail"/>).
    /// </remarks>
    [Fact]
    public async Task UpdateRefusesAnInvalidStatus()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(status: "NotAStatus"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Updating a client is not for anonymous callers.</summary>
    [Fact]
    public async Task UpdateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}", ClientsTestSeeds.UpdateBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    [Fact]
    public async Task UpdateRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "update-forbidden");

        var response = await client.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Moving a client's sign-in to an address another account holds answers 409
    /// <c>EMAIL_ALREADY_IN_USE</c> instead of a 500, and nothing is saved: the profile and the
    /// sign-in address stay as they were.
    /// </summary>
    [Fact]
    public async Task UpdateRefusesAnEmailAnotherAccountHolds()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "admin-email-taken");
        var (_, takenEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "admin-email-holder");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Changed", email: takenEmail));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("firstName").GetString().Should().NotBe("Changed");
        (await client.GetAsync("/api/clients/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A save that repeats the address the account already signs in with changes nothing about the
    /// account: the client's session stays valid.
    /// </summary>
    [Fact]
    public async Task UpdateWithTheSameEmailKeepsTheClientSessions()
    {
        var signedIn = await ClientsTestSeeds.RegisterClientAsync(factory, "admin-email-same");
        var (client, clientId) = (signedIn.Client, signedIn.ClientId);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(firstName: "Renamed", email: signedIn.Email.ToUpperInvariant()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/clients/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A malformed address is refused as a validation error before anything is read or written.</summary>
    [Fact]
    public async Task UpdateRefusesAMalformedEmail()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(email: "not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A save leaves one activity line naming the client and the acting admin, one more when the notes
    /// changed and one when the status changed, and none of them carries what was written.
    /// </summary>
    [Fact]
    public async Task UpdateIsAuditedWithoutPersonalData()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var body = ClientsTestSeeds.UpdateBody(firstName: "Katherine", status: "Suspended", adminNotes: "private note about the customer");

        (await admin.PutAsJsonAsync($"/api/clients/{clientId}", body)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Modified Client - Client ID");
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Modified Client Notes");
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Changed Client Status");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var lines = await db.ActivityLogs.AsNoTracking().Where(l => l.ClientId == clientId).ToListAsync();
        lines.Should().OnlyContain(l => !l.Description.Contains("private note") && !l.Description.Contains("Katherine"));
    }

    /// <summary>An email that is not a well-formed address fails validation.</summary>
    /// <param name="email">An address that is not a well-formed email address.</param>
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b@c.example")]
    public void MalformedEmail_ShouldFail(string email)
    {
        _validator.TestValidate(MakeValidCommand(email: email)).ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>An email longer than 256 characters fails validation even when it is well formed.</summary>
    [Fact]
    public void OverlongEmail_ShouldFail()
    {
        var email = new string('a', 250) + "@example.test";

        _validator.TestValidate(MakeValidCommand(email: email)).ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>A status that is a member of the enum passes.</summary>
    [Fact]
    public void DefinedStatus_ShouldPass()
    {
        _validator.TestValidate(MakeValidCommand(status: ClientStatus.Suspended)).ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    /// <summary>A number the JSON binder accepts but that names no member of the enum fails.</summary>
    [Fact]
    public void UndefinedStatus_ShouldFail()
    {
        _validator.TestValidate(MakeValidCommand(status: (ClientStatus)99)).ShouldHaveValidationErrorFor(x => x.Status);
    }

    /// <summary>A blank email means "keep the address the account has" and passes.</summary>
    [Fact]
    public void BlankEmail_ShouldPass()
    {
        _validator.TestValidate(MakeValidCommand(email: null)).ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>Validator instance under test.</summary>
    private readonly UpdateClientValidator _validator = new(new EchoLocalizer<ApplicationValidationMessages>());

    /// <summary>Creates a valid command with sensible defaults for all fields.</summary>
    /// <param name="clientId">The client id to put on the command.</param>
    /// <param name="email">The email to put on the command.</param>
    /// <param name="firstName">The first name to put on the command.</param>
    /// <param name="lastName">The last name to put on the command.</param>
    /// <param name="companyName">The company name to put on the command.</param>
    /// <param name="phone">The phone number to put on the command.</param>
    /// <param name="street">The street line to put on the command.</param>
    /// <param name="address2">The second address line to put on the command.</param>
    /// <param name="city">The city to put on the command.</param>
    /// <param name="state">The state or province to put on the command.</param>
    /// <param name="postCode">The postal code to put on the command.</param>
    /// <param name="country">The ISO country code to put on the command.</param>
    /// <param name="language">The locale tag to put on the command.</param>
    /// <param name="currency">The currency code to put on the command.</param>
    /// <param name="paymentMethod">The payment method id to put on the command.</param>
    /// <param name="billingContact">The billing contact to put on the command.</param>
    /// <param name="adminNotes">The admin notes to put on the command.</param>
    /// <param name="status">The client status to put on the command.</param>
    /// <returns>The command.</returns>
    private static UpdateClientCommand MakeValidCommand(
        int clientId = 1,
        string? email = "john@example.com",
        string firstName = "John",
        string lastName = "Doe",
        string? companyName = null,
        string? phone = null,
        string? street = null,
        string? address2 = null,
        string? city = null,
        string? state = null,
        string? postCode = null,
        string? country = null,
        string? language = null,
        string? currency = null,
        string? paymentMethod = null,
        string? billingContact = null,
        string? adminNotes = null,
        ClientStatus? status = null) =>
        new(
            clientId, email, firstName, lastName, companyName, phone,
            new PostalAddress(street, address2, city, state, postCode, country),
            new ClientPreferencesDto(language, currency, paymentMethod, billingContact, adminNotes),
            new ContactNotifications(true, true, true, true, true, true),
            new ClientSettingsDto(true, true, false, false, false, true, true),
            status);

    /// <summary>Valid command should pass.</summary>
    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = MakeValidCommand();
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>ClientId 0 should fail.</summary>
    [Fact]
    public void ZeroClientId_ShouldFail()
    {
        var cmd = MakeValidCommand(clientId: 0);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    /// <summary>
    /// A language outside <c>LocaleOptions.SupportedLocales</c> should fail. Stored, it would
    /// be read back and then served English, which looks exactly like the save not working.
    /// </summary>
    [Fact]
    public void UnsupportedLanguage_ShouldFail()
    {
        var cmd = MakeValidCommand(language: "english");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Preferences.Language);
    }

    /// <summary>A supported language code should pass, whatever its casing.</summary>
    /// <param name="language">The language tag under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("HY")]
    public void SupportedLanguage_ShouldPass(string language)
    {
        var cmd = MakeValidCommand(language: language);
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Preferences.Language);
    }

    /// <summary>
    /// Blank means "no preference" and must pass: the account form posts an empty string for
    /// its default option, and some callers send null for the same thing.
    /// </summary>
    /// <param name="language">The blank value under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BlankLanguage_ShouldPass(string? language)
    {
        var cmd = MakeValidCommand(language: language);
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Preferences.Language);
    }

    /// <summary>Country code with wrong length should fail.</summary>
    [Fact]
    public void WrongLengthCountry_ShouldFail()
    {
        var cmd = MakeValidCommand(country: "USA");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Address.Country);
    }

    /// <summary>Empty FirstName should fail.</summary>
    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var cmd = MakeValidCommand(firstName: "");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}
