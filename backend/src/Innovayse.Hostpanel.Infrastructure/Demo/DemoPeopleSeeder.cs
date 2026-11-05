using System.Security.Cryptography;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the people: the published demo administrator and client, staff with different roles, the demo
/// client's full profile (contacts, team members, invitations, a saved-card customer and a credit
/// balance), and sixty-four more fictional customers in mixed states so lists, charts and reports have
/// something to show. Accounts are created through Identity (so the stored hash is the real one), the
/// role of each is also granted in the subject-role table the authorization reads, and clients are built
/// through <see cref="Client.Create"/> and its behaviour methods; an account or client that already exists
/// is left alone.
/// </summary>
internal sealed class DemoPeopleSeeder
{
    /// <summary>Prefix that guarantees upper, lower, digit and symbol characters in a generated password.</summary>
    private const string GeneratedPasswordPrefix = "Aa1!";

    /// <summary>The domain the unpublished accounts' addresses sit under.</summary>
    private const string FillerMailDomain = "demo.example";

    /// <summary>Everything the demo client's team member may see except money and account settings.</summary>
    private const ClientPermission OperationsPermissions =
        ClientPermission.ViewProductsServices | ClientPermission.ViewDomains | ClientPermission.ManageDomainSettings
        | ClientPermission.ViewOpenSupportTickets | ClientPermission.ViewEmails;

    /// <summary>What the demo client's finance team member may do.</summary>
    private const ClientPermission FinancePermissions =
        ClientPermission.ViewPayInvoices | ClientPermission.ViewAcceptQuotes | ClientPermission.ViewEmails;

    /// <summary>The data context the clients are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>Creates and looks up accounts.</summary>
    private readonly UserManager<AppUser> _users;

    /// <summary>The published demo accounts.</summary>
    private readonly DemoOptions _options;

    /// <summary>The anchor signup dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the clients are added to and saved through.</param>
    /// <param name="users">Creates and looks up accounts.</param>
    /// <param name="options">The published demo accounts.</param>
    /// <param name="today">The anchor signup dates are measured from.</param>
    public DemoPeopleSeeder(AppDbContext db, UserManager<AppUser> users, DemoOptions options, DateTimeOffset today)
    {
        _db = db;
        _users = users;
        _options = options;
        _today = today;
    }

    /// <summary>Seeds the administrator, the staff and the clients that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The id of the published demo client, for the seeders that attach data to it.</returns>
    public async Task<int> SeedAsync(CancellationToken ct)
    {
        await EnsureAccountAsync(_options.AdminEmail!, _options.AdminPassword!, "Demo", "Admin", Role.Admin, ct);
        await SeedStaffAsync(ct);

        var demoProfile = new DemoClientProfile(
            _options.ClientEmail!, _options.ClientPassword!, "Demo", "Client", "Brightleaf Studio LLC",
            "+1 555 0110", "15 Harbour Street", "Northvale", null, "00001", "US", "USD",
            ClientStatus.Active, 120m);
        var (demoClient, demoCreated) = await EnsureClientAsync(demoProfile, DemoAnchor.SignupDate(0, _today), ct);
        if (demoCreated)
        {
            await CompleteDemoClientAsync(demoClient, ct);
            await ApplyStatusAsync(demoClient, demoProfile.Status, ct);
        }

        (string First, string Last, string Company, string Phone, string Street, string City, string? State, string PostCode, string Country, string Currency, ClientStatus Status, decimal Credit)[] fillers =
        [
            ("Maple", "Digital", "Maple Digital Inc", "+1 555 0120", "20 Linden Avenue", "Northvale", null, "00019", "US", "EUR", ClientStatus.Active, 0m),
            ("Harper", "Lane", "Lane & Co Consulting", "+1 212 555 0130", "480 Hudson Street", "New York", "NY", "10014", "US", "USD", ClientStatus.Active, 40m),
            ("Sofia", "Rinaldi", "Rinaldi Design Studio", "+39 06 555 0140", "Via Roma 12", "Rome", null, "00184", "IT", "USD", ClientStatus.Active, 0m),
            ("Kenji", "Mori", "Mori Web Works", "+81 3 5550 0150", "2-4-1 Shibuya", "Tokyo", null, "150-0002", "JP", "USD", ClientStatus.Active, 0m),
            ("Nora", "Sanders", "Cloudline Systems LLC", "+1 555 0130", "7 Pine Avenue", "Northvale", null, "00010", "US", "EUR", ClientStatus.Suspended, 0m),
            ("Mark", "Brennan", "Brennan Media Group", "+44 20 7946 0140", "22 Baker Lane", "London", null, "NW1 6XE", "GB", "USD", ClientStatus.Active, 0m),
            ("Lena", "Fischer", "Fischer Analytics GmbH", "+49 30 5550 0160", "Torstrasse 101", "Berlin", null, "10119", "DE", "USD", ClientStatus.Active, 25m),
            ("Omar", "Haddad", "Haddad Logistics", "+971 4 555 0170", "Sheikh Zayed Road 18", "Dubai", null, "00000", "AE", "USD", ClientStatus.Active, 0m),
            ("Elena", "Petrova", "Petrova Interiors", "+7 495 555 0180", "Tverskaya 9", "Moscow", null, "125009", "RU", "USD", ClientStatus.Closed, 0m),
            ("Daniel", "Okafor", "Okafor Ventures Ltd", "+234 1 555 0190", "14 Marina Road", "Lagos", null, "101241", "NG", "USD", ClientStatus.Active, 0m),
            ("Anna", "Grayson", "Grayson Dental Clinic", "+1 555 0140", "3 Mill Street", "Lakeside", null, "00031", "US", "EUR", ClientStatus.Active, 40m),
            ("Tomas", "Novak", "Novak Craft Brewery", "+420 2 5550 0200", "Narodni 25", "Prague", null, "11000", "CZ", "USD", ClientStatus.Suspended, 0m),
            ("Isabel", "Costa", "Costa Travel Agency", "+351 21 555 0210", "Rua Augusta 100", "Lisbon", null, "1100-053", "PT", "USD", ClientStatus.Active, 0m),
            ("Maya", "Patel", "Patel Software Pvt", "+91 22 5550 0220", "Linking Road 5", "Mumbai", "MH", "400050", "IN", "USD", ClientStatus.Active, 0m),
            ("Liam", "Carter", "Carterline Builders", "+1 555 0230", "41 Mill Road", "Northvale", null, "00041", "US", "USD", ClientStatus.Active, 0m),
            ("Chloe", "Dubois", "Dubois Bakery", "+33 1 5550 0240", "8 Rue des Lilas", "Paris", null, "75002", "FR", "USD", ClientStatus.Active, 10m),
            ("Arman", "Sargsyan", "Apricot Tours", "+49 30 5550 024", "5 Orchard Lane", "Fairhaven", null, "10010", "DE", "EUR", ClientStatus.Active, 0m),
            ("Hugo", "Lindqvist", "Northstar Outdoor AB", "+46 8 5550 0250", "Hamngatan 3", "Stockholm", null, "11122", "SE", "USD", ClientStatus.Active, 0m),
            ("Priya", "Nair", "Lotus Wellness", "+91 80 5550 0260", "MG Road 77", "Bengaluru", "KA", "560001", "IN", "USD", ClientStatus.Active, 0m),
            ("Mateo", "Garcia", "Solana Coffee Roasters", "+34 91 555 0270", "Calle Mayor 9", "Madrid", null, "28013", "ES", "USD", ClientStatus.Active, 0m),
            ("Ava", "Thompson", "Quillstone Publishing", "+44 161 555 0280", "12 Kings Row", "Manchester", null, "M1 1AA", "GB", "USD", ClientStatus.Active, 0m),
            ("Noah", "Becker", "Bergwerk Engineering", "+49 89 5550 0290", "Lindenweg 4", "Munich", null, "80331", "DE", "USD", ClientStatus.Suspended, 0m),
            ("Zara", "Ahmed", "Oasis Events", "+971 2 555 0300", "Corniche Road 6", "Abu Dhabi", null, "00000", "AE", "USD", ClientStatus.Active, 0m),
            ("Lucas", "Silva", "Verdant Farms", "+55 11 5550 0310", "Rua das Flores 50", "Sao Paulo", "SP", "01000-000", "BR", "USD", ClientStatus.Active, 0m),
            ("Emma", "Johansen", "Fjelltopp Cabins", "+47 22 55 50 320", "Fjordveien 2", "Oslo", null, "0150", "NO", "USD", ClientStatus.Active, 0m),
            ("Ethan", "Walsh", "Tidewater Marine", "+353 1 555 0330", "3 Custom House Quay", "Dublin", null, "D02 XY00", "IE", "USD", ClientStatus.Active, 0m),
            ("Sofia", "Kowalska", "Amberline Studio", "+48 22 555 0340", "ul. Ogrodowa 7", "Warsaw", null, "00-001", "PL", "USD", ClientStatus.Active, 0m),
            ("Leo", "Martins", "Atlantico Surf School", "+351 22 555 0350", "Avenida da Praia 15", "Porto", null, "4000-001", "PT", "USD", ClientStatus.Closed, 0m),
            ("Hana", "Suzuki", "Sakura Tea House", "+81 6 5550 0360", "1-2-3 Namba", "Osaka", null, "542-0076", "JP", "USD", ClientStatus.Active, 0m),
            ("Oliver", "Grant", "Summit Legal Partners", "+1 312 555 0370", "200 Lakeshore Plaza", "Chicago", "IL", "60601", "US", "USD", ClientStatus.Active, 75m),
            ("Ani", "Hakobyan", "Pomegranate Crafts", "+49 30 5550 038", "9 Linden Avenue", "Fairhaven", null, "10025", "DE", "EUR", ClientStatus.Active, 20m),
            ("Jack", "Murphy", "Ironbridge Fitness", "+1 555 0390", "88 Riverside Drive", "Lakeside", null, "00039", "US", "USD", ClientStatus.Active, 0m),
            ("Mila", "Horvat", "Adriatic Yachting", "+385 1 555 0400", "Ilica 11", "Zagreb", null, "10000", "HR", "USD", ClientStatus.Active, 0m),
            ("Ryan", "Cooper", "Copperleaf Accounting", "+1 415 555 0410", "500 Market Street", "San Francisco", "CA", "94105", "US", "USD", ClientStatus.Active, 0m),
            ("Leila", "Rahimi", "Saffron Kitchen", "+1 555 0420", "6 Elm Court", "Northvale", null, "00042", "US", "USD", ClientStatus.Active, 0m),
            ("Felix", "Wagner", "Alpenglow Hotels", "+43 1 555 0430", "Lindengasse 5", "Vienna", null, "1010", "AT", "USD", ClientStatus.Active, 0m),
            ("Grace", "Kim", "Hanbit Robotics", "+82 2 555 0440", "Hangang-daero 21", "Seoul", null, "04524", "KR", "USD", ClientStatus.Active, 0m),
            ("Victor", "Ionescu", "Danube Freight", "+40 21 555 0450", "Strada Florilor 4", "Bucharest", null, "010011", "RO", "USD", ClientStatus.Suspended, 0m),
            ("Isla", "Brown", "Heather Knitwear", "+44 131 555 0460", "7 Abbey Close", "Edinburgh", null, "EH1 1AA", "GB", "USD", ClientStatus.Active, 0m),
            ("Ella", "Novik", "Birchwood Bakery", "+1 555 0470", "100 Harbour Street", "Fairhaven", null, "50000", "US", "USD", ClientStatus.Active, 0m),
            ("Max", "Ferreira", "Cascade Plumbing", "+44 20 5550 0480", "101 Harbour Street", "Fairhaven", null, "50001", "GB", "USD", ClientStatus.Active, 0m),
            ("Ruby", "Lam", "Driftwood Gallery", "+49 30 5550 0490", "102 Harbour Street", "Fairhaven", null, "50002", "DE", "USD", ClientStatus.Active, 0m),
            ("Sam", "Okoro", "Evergreen Landscaping", "+49 30 5550 0500", "103 Harbour Street", "Fairhaven", null, "50003", "DE", "EUR", ClientStatus.Active, 50m),
            ("Lily", "Rossi", "Foxglove Florist", "+33 1 5550 0510", "104 Harbour Street", "Fairhaven", null, "50004", "FR", "USD", ClientStatus.Active, 0m),
            ("Adam", "Kerr", "Glacier Water Co", "+1 555 0520", "105 Harbour Street", "Fairhaven", null, "50005", "US", "USD", ClientStatus.Active, 0m),
            ("Nina", "Vos", "Hollow Oak Furniture", "+44 20 5550 0530", "106 Harbour Street", "Fairhaven", null, "50006", "GB", "USD", ClientStatus.Suspended, 0m),
            ("Theo", "Mendes", "Indigo Print Shop", "+49 30 5550 0540", "107 Harbour Street", "Fairhaven", null, "50007", "DE", "USD", ClientStatus.Active, 0m),
            ("Iris", "Holm", "Jasmine Spa", "+49 30 5550 0550", "108 Harbour Street", "Fairhaven", null, "50008", "DE", "EUR", ClientStatus.Active, 0m),
            ("Jonas", "Weber", "Keystone Realty", "+33 1 5550 0560", "109 Harbour Street", "Fairhaven", null, "50009", "FR", "USD", ClientStatus.Active, 0m),
            ("Clara", "Moretti", "Larkspur Music School", "+1 555 0570", "110 Harbour Street", "Fairhaven", null, "50010", "US", "USD", ClientStatus.Active, 0m),
            ("Owen", "Price", "Moonrise Cinema", "+44 20 5550 0580", "111 Harbour Street", "Fairhaven", null, "50011", "GB", "USD", ClientStatus.Active, 50m),
            ("Eva", "Lund", "Nettle Organic Market", "+49 30 5550 0590", "112 Harbour Street", "Fairhaven", null, "50012", "DE", "USD", ClientStatus.Active, 0m),
            ("Paul", "Dimitrov", "Obsidian Games", "+49 30 5550 0600", "113 Harbour Street", "Fairhaven", null, "50013", "DE", "EUR", ClientStatus.Active, 0m),
            ("Rosa", "Vega", "Pebble Pet Care", "+33 1 5550 0610", "114 Harbour Street", "Fairhaven", null, "50014", "FR", "USD", ClientStatus.Active, 0m),
            ("Kai", "Tanaka", "Quartz Electronics", "+1 555 0620", "115 Harbour Street", "Fairhaven", null, "50015", "US", "USD", ClientStatus.Active, 0m),
            ("Lea", "Brun", "Redwood Dental", "+44 20 5550 0630", "116 Harbour Street", "Fairhaven", null, "50016", "GB", "USD", ClientStatus.Active, 0m),
            ("Ben", "Ali", "Sapphire Jewelry", "+49 30 5550 0640", "117 Harbour Street", "Fairhaven", null, "50017", "DE", "USD", ClientStatus.Closed, 0m),
            ("Mia", "Hansen", "Thistle Tailoring", "+49 30 5550 0650", "118 Harbour Street", "Fairhaven", null, "50018", "DE", "EUR", ClientStatus.Active, 0m),
            ("Dan", "Popescu", "Umber Coffee Bar", "+33 1 5550 0660", "119 Harbour Street", "Fairhaven", null, "50019", "FR", "USD", ClientStatus.Active, 50m),
            ("Tara", "Quinn", "Vellum Bookbinders", "+1 555 0670", "120 Harbour Street", "Fairhaven", null, "50020", "US", "USD", ClientStatus.Active, 0m),
            ("Ian", "Reid", "Willow Daycare", "+44 20 5550 0680", "121 Harbour Street", "Fairhaven", null, "50021", "GB", "USD", ClientStatus.Suspended, 0m),
            ("Lina", "Saleh", "Yarrow Herbal", "+49 30 5550 0690", "122 Harbour Street", "Fairhaven", null, "50022", "DE", "USD", ClientStatus.Active, 0m),
            ("Erik", "Strand", "Zephyr Cycling", "+49 30 5550 0700", "123 Harbour Street", "Fairhaven", null, "50023", "DE", "EUR", ClientStatus.Active, 0m),
            ("Nora", "Paz", "Aspen Ski Rentals", "+33 1 5550 0710", "124 Harbour Street", "Fairhaven", null, "50024", "FR", "USD", ClientStatus.Active, 0m),
        ];

        for (var i = 0; i < fillers.Length; i++)
        {
            var (first, last, company, phone, street, city, state, postCode, country, currency, status, credit) = fillers[i];
            var email = $"{first}.{last}@{FillerMailDomain}".ToLowerInvariant();
            var profile = new DemoClientProfile(
                email, NewGeneratedPassword(), first, last, company, phone, street, city, state, postCode, country, currency,
                status, credit);
            var (client, created) = await EnsureClientAsync(profile, DemoAnchor.SignupDate(i + 1, _today), ct);

            if (created)
            {
                await CompleteFillerAsync(client, i, ct);
                await ApplyStatusAsync(client, profile.Status, ct);
            }
        }

        return demoClient.Id;
    }

    /// <summary>
    /// A fresh random password for an account nobody is meant to sign in as; it is never stored or shown,
    /// and no secret sits in the source. A guessable password here would be a hole in a public demo.
    /// </summary>
    /// <returns>A random password that satisfies the identity password policy.</returns>
    private static string NewGeneratedPassword()
    {
        return GeneratedPasswordPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>Creates the staff accounts: two administrators and two resellers.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the accounts exist.</returns>
    private async Task SeedStaffAsync(CancellationToken ct)
    {
        (string Local, string First, string Last, Role Role)[] staff =
        [
            ("support.lead", "Rafael", "Torres", Role.Admin),
            ("billing.manager", "Julia", "Weiss", Role.Admin),
            ("partner.north", "Peter", "Hall", Role.Reseller),
            ("partner.south", "Mila", "Stone", Role.Reseller),
        ];

        foreach (var (local, first, last, role) in staff)
        {
            await EnsureAccountAsync($"{local}@{FillerMailDomain}", NewGeneratedPassword(), first, last, role, ct);
        }
    }

    /// <summary>
    /// Gives the published demo client everything a full profile has: billing preferences, notification and
    /// billing settings, a card-gateway customer for the saved sample card, a credit balance, technical and
    /// general contacts, two team members with different permissions and three invitations.
    /// </summary>
    /// <param name="client">The demo client, already saved.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task CompleteDemoClientAsync(Client client, CancellationToken ct)
    {
        client.UpdatePreferences(
            PaymentMethodNames.Card, "Demo Client", "Long-standing customer; prefers invoices by email and pays by card.");
        client.UpdateNotifications(true, true, true, true, true, false);
        client.UpdateSettings(true, false, false, false, true, true, true);

        client.AddContact(new ContactDetails(
            "Nina", "Avery", "Brightleaf Studio LLC", $"nina.avery@{FillerMailDomain}", "+1 555 0111", ContactType.Technical,
            new PostalAddress("15 Harbour Street", null, "Northvale", null, "00001", "US"),
            new ContactNotifications(true, false, true, true, true, false)));
        client.AddContact(new ContactDetails(
            "Gary", "Sawyer", "Brightleaf Studio LLC", $"gary.sawyer@{FillerMailDomain}", "+1 555 0112", ContactType.General,
            new PostalAddress("15 Harbour Street", null, "Northvale", null, "00001", "US"),
            new ContactNotifications(true, false, false, false, false, false)));

        var editor = await EnsureAccountAsync(
            $"team.editor@{FillerMailDomain}", NewGeneratedPassword(), "Taylor", "Editor", Role.Client, ct);
        var finance = await EnsureAccountAsync(
            $"team.finance@{FillerMailDomain}", NewGeneratedPassword(), "Lusine", "Finance", Role.Client, ct);
        client.AddUser(editor.Id, OperationsPermissions);
        client.AddUser(finance.Id, FinancePermissions);
        await _db.SaveChangesAsync(ct);

        // One invitation already accepted (the editor above), two still waiting for an answer.
        var accepted = Invitation.Create(client.Id, editor.Email!, "Taylor", "Editor", OperationsPermissions, _today);
        accepted.Accept(_today);
        _db.Invitations.Add(accepted);
        _db.Invitations.Add(Invitation.Create(client.Id, $"design.lead@{FillerMailDomain}", "Mari", "Designer", ClientPermission.ViewProductsServices, _today));
        _db.Invitations.Add(Invitation.Create(client.Id, $"auditor@{FillerMailDomain}", "Karen", "Auditor", FinancePermissions, _today));
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Gives a filler customer the extras that vary by position: a technical contact, a team member, an invitation.</summary>
    /// <param name="client">The customer, already saved.</param>
    /// <param name="index">The customer's position in the filler list.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task CompleteFillerAsync(Client client, int index, CancellationToken ct)
    {
        if (index % 2 == 0)
        {
            client.AddContact(new ContactDetails(
                "Operations", "Team", client.CompanyName, $"ops.{index}@{FillerMailDomain}", client.Phone, ContactType.Technical,
                new PostalAddress(client.Street, null, client.City, client.State, client.PostCode, client.Country),
                new ContactNotifications(true, false, true, true, true, false)));
        }

        if (index == 1)
        {
            var member = await EnsureAccountAsync(
                $"team.lane@{FillerMailDomain}", NewGeneratedPassword(), "Owen", "Lane", Role.Client, ct);
            client.AddUser(member.Id, ClientPermission.ViewProductsServices | ClientPermission.ViewPayInvoices);
            _db.Invitations.Add(Invitation.Create(client.Id, $"bookkeeper.lane@{FillerMailDomain}", "Ruth", "Keeper", FinancePermissions, _today));
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Creates an account with a role when no account has the email; otherwise syncs its password to the configured one.</summary>
    /// <param name="email">Sign-in email.</param>
    /// <param name="password">Sign-in password.</param>
    /// <param name="firstName">Given name.</param>
    /// <param name="lastName">Family name.</param>
    /// <param name="role">The role to grant.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The existing (password synced) or the new account.</returns>
    /// <exception cref="InvalidOperationException">Identity refused the account, the password or the role.</exception>
    private async Task<AppUser> EnsureAccountAsync(
        string email, string password, string firstName, string lastName, Role role, CancellationToken ct)
    {
        var existing = await _users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Keep the account on the configured password, which may have changed since it was created.
            if (!await _users.CheckPasswordAsync(existing, password))
            {
                var token = await _users.GeneratePasswordResetTokenAsync(existing);
                var reset = await _users.ResetPasswordAsync(existing, token, password);
                if (!reset.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"The demo account {email} password could not be synced: {string.Join("; ", reset.Errors.Select(e => e.Code))}.");
                }
            }

            await GrantSubjectRoleAsync(existing.Id, role, ct);
            return existing;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"The demo account {email} could not be created: {string.Join("; ", created.Errors.Select(e => e.Code))}.");
        }

        var granted = await _users.AddToRoleAsync(user, role.ToString());
        if (!granted.Succeeded)
        {
            throw new InvalidOperationException(
                $"The demo account {email} could not be given the role {role}: {string.Join("; ", granted.Errors.Select(e => e.Code))}.");
        }

        await GrantSubjectRoleAsync(user.Id, role, ct);
        return user;
    }

    /// <summary>Records the role in the subject-role table the authorization reads, unless the subject already holds it.</summary>
    /// <param name="subject">The account id.</param>
    /// <param name="role">The role to grant.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    private async Task GrantSubjectRoleAsync(string subject, Role role, CancellationToken ct)
    {
        if (await _db.SubjectRoles.AnyAsync(r => r.Subject == subject && r.Role == role, ct))
        {
            return;
        }

        _db.SubjectRoles.Add(SubjectRole.Grant(subject, role));
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates a client with its account, address, billing contact and credit when it does not exist yet. The
    /// profile's account state is applied by <see cref="ApplyStatusAsync"/> once the caller has added the rest.
    /// </summary>
    /// <param name="profile">What to create.</param>
    /// <param name="signedUpAt">The moment the customer signed up, so the account is dated like the history that follows it.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The client, and whether this call created it.</returns>
    private async Task<DemoSeededClient> EnsureClientAsync(DemoClientProfile profile, DateTimeOffset signedUpAt, CancellationToken ct)
    {
        var user = await EnsureAccountAsync(profile.Email, profile.Password, profile.FirstName, profile.LastName, Role.Client, ct);

        var existing = await _db.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id, ct);
        if (existing is not null)
        {
            return new DemoSeededClient(existing, false);
        }

        var client = Client.Create(user.Id, profile.FirstName, profile.LastName, profile.Email, _today, profile.Company);
        client.Update(profile.FirstName, profile.LastName, profile.Company, profile.Phone);
        client.UpdateAddress(profile.Street, null, profile.City, profile.State, profile.PostCode, profile.Country);
        client.SetCurrency(profile.Currency);
        client.Backdate(signedUpAt);
        if (profile.Credit > 0)
        {
            client.AddCredit(profile.Credit);
        }

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        client.AddContact(new ContactDetails(
            profile.FirstName, profile.LastName, profile.Company, profile.Email, profile.Phone, ContactType.Billing,
            new PostalAddress(null, null, null, null, null, null),
            new ContactNotifications(true, true, true, true, true, true)));
        await _db.SaveChangesAsync(ct);

        return new DemoSeededClient(client, true);
    }

    /// <summary>
    /// Puts a seeded customer in the account state it ends up in. Called once its contacts, users and
    /// invitations are in place: a suspended or closed account takes no further changes of ours.
    /// </summary>
    /// <param name="client">The customer, fully seeded.</param>
    /// <param name="status">The account state to end in; <see cref="ClientStatus.Active"/> leaves it as created.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the state is saved.</returns>
    private async Task ApplyStatusAsync(Client client, ClientStatus status, CancellationToken ct)
    {
        if (status == ClientStatus.Suspended)
        {
            client.Suspend(_today);
        }
        else if (status == ClientStatus.Closed)
        {
            client.Close(_today);
        }

        await _db.SaveChangesAsync(ct);
    }
}
