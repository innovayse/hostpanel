using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the registered domains in every lifecycle state (active, expiring soon, pending registration, pending
/// transfer, expired, in redemption, transferred away, cancelled) with the things a managed domain carries: name
/// servers, a registrar lock, DNS records, email forwarding rules and the expiry reminders already sent. The state
/// of each domain follows a fixed table and the state of its owner, and every date is counted from the anchor.
/// </summary>
internal sealed class DemoDomainSeeder
{
    /// <summary>The registrar name shown on every domain.</summary>
    private const string RegistrarName = "Demo Registrar";

    /// <summary>The documentation address range the sample A records point into; it never routes anywhere.</summary>
    private const string AddressPrefix = "203.0.113.";

    /// <summary>Domains at or under this many days from expiry get reminders.</summary>
    private const int ReminderWindowDays = 30;

    /// <summary>The state and distance to expiry of the n-th domain handed out to the customers after the demo client, repeating.</summary>
    private static readonly DemoDomainKind[] KindPattern =
    [
        new(DomainStatus.Active, 240), new(DomainStatus.Active, 120), new(DomainStatus.Active, 9), new(DomainStatus.Active, 330),
        new(DomainStatus.PendingTransfer, 0), new(DomainStatus.Active, 75), new(DomainStatus.Expired, -12), new(DomainStatus.Active, 180),
        new(DomainStatus.Redemption, -45), new(DomainStatus.Active, 22), new(DomainStatus.Transferred, 200), new(DomainStatus.Active, 300),
        new(DomainStatus.Cancelled, -60), new(DomainStatus.Active, 150), new(DomainStatus.PendingRegistration, 365), new(DomainStatus.Active, 27),
    ];

    /// <summary>The demo client's own domains: suffix, state and distance to expiry.</summary>
    private static readonly DemoClientDomain[] DemoClientDomains =
    [
        new(".example", DomainStatus.Active, 240),
        new("-net.example", DomainStatus.Active, 10),
        new("-io.example", DomainStatus.PendingTransfer, 0),
        new("-org.example", DomainStatus.Active, 150),
        new("-dev.example", DomainStatus.Expired, -12),
        new("-app.example", DomainStatus.PendingRegistration, 365),
        new("-co.example", DomainStatus.Active, 22),
        new("-shop.example", DomainStatus.Active, 300),
        new("-old.example", DomainStatus.Redemption, -45),
        new("-moved.example", DomainStatus.Transferred, 200),
        new("-closed.example", DomainStatus.Cancelled, -60),
        new("-blog.example", DomainStatus.Active, 90),
    ];

    /// <summary>The suffixes (all under the reserved example name) the other customers' second domains use, rotated.</summary>
    private static readonly string[] ExtraExtensions = ["-net.example", "-org.example", "-io.example", "-co.example", "-dev.example", "-app.example"];

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoDomainSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds the domains that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (await _db.Domains.AnyAsync(ct))
        {
            return;
        }

        var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync(ct);
        var sequence = 0;
        var pattern = 0;

        for (var c = 0; c < clients.Count; c++)
        {
            var client = clients[c];
            if (c == 0)
            {
                foreach (var (extension, status, days) in DemoClientDomains)
                {
                    AddDomain(client, DemoSites.LabelOf(client) + extension, status, days, sequence);
                    sequence++;
                }

                continue;
            }

            // Every customer has a main domain; every other one has a second.
            var count = c % 2 == 1 ? 2 : 1;
            for (var j = 0; j < count; j++)
            {
                var (status, days) = KindPattern[pattern % KindPattern.Length];
                pattern++;
                var name = j == 0
                    ? DemoSites.HostOf(client)
                    : DemoSites.LabelOf(client) + ExtraExtensions[c % ExtraExtensions.Length];
                AddDomain(client, name, status, days, sequence);
                sequence++;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Builds one domain in the requested state, with its configuration, and adds it to the context.</summary>
    /// <param name="client">The owner.</param>
    /// <param name="name">The fully-qualified domain name.</param>
    /// <param name="requested">The state the domain should end up in; a closed customer's domains are cancelled instead.</param>
    /// <param name="expiresInDays">Days from the anchor to expiry; negative for a domain already past it.</param>
    /// <param name="sequence">The domain's running number, used to vary the figures deterministically.</param>
    private void AddDomain(Client client, string name, DomainStatus requested, int expiresInDays, int sequence)
    {
        var status = client.Status == ClientStatus.Closed ? DomainStatus.Cancelled : requested;
        var currency = client.Currency ?? DemoCatalogueSeeder.BaseCurrency;
        var price = currency == DemoCatalogueSeeder.BaseCurrency ? 11.99m : 4600m;
        var expiresAt = _today.AddDays(expiresInDays);

        var domain = status == DomainStatus.PendingTransfer
            ? RegisteredDomain.CreateTransfer(client.Id, name, _today, currency, price, price, PaymentMethodNames.Card)
            : RegisteredDomain.Register(
                client.Id, name, _today, expiresAt, autoRenew: sequence % 4 != 3, whoisPrivacy: sequence % 2 == 0,
                recurringAmount: price, firstPaymentAmount: price, priceCurrency: currency,
                registrar: RegistrarName, registrationPeriod: 1, paymentMethod: PaymentMethodNames.Card);

        switch (status)
        {
            case DomainStatus.PendingTransfer:
                domain.SetEppCode($"DEMO-EPP-{1000 + sequence}");
                break;
            case DomainStatus.Cancelled:
                domain.Cancel();
                break;
            case DomainStatus.PendingRegistration:
                break;
            default:
                MoveToLiveState(domain, status, sequence);
                break;
        }

        // Registered a year before it expires (or when the owner signed up, if that was later); a domain still waiting
        // to be registered or transferred was ordered just now and keeps today's date.
        if (status is not (DomainStatus.PendingTransfer or DomainStatus.PendingRegistration))
        {
            var anniversary = expiresAt.AddYears(-1);
            var earliest = DemoAnchor.Clamp(client.CreatedAt.AddDays(1 + (sequence % 5)), client.CreatedAt, _today);
            domain.Backdate(DemoAnchor.Clamp(anniversary, earliest, _today));
        }

        var configured = status is DomainStatus.Active or DomainStatus.Expired or DomainStatus.Redemption or DomainStatus.Transferred;
        if (configured)
        {
            domain.SetLock(status == DomainStatus.Active);
            domain.SetNameservers(["ns1.demo-dns.example", "ns2.demo-dns.example"]);
        }

        if (status == DomainStatus.Active)
        {
            AddZone(domain, name, sequence);
            AddReminders(domain, name, expiresInDays);
        }
        else if (status is DomainStatus.Expired or DomainStatus.Redemption)
        {
            domain.AddReminder(status == DomainStatus.Expired ? DomainReminderType.ExpiredNotice : DomainReminderType.RedemptionNotice, $"admin@{name}", _today);
        }

        _db.Domains.Add(domain);
    }

    /// <summary>Walks a freshly registered domain to a state past registration.</summary>
    /// <param name="domain">The domain, still pending registration.</param>
    /// <param name="status">The state to reach: active, expired, in redemption or transferred away.</param>
    /// <param name="sequence">The domain's running number, used in the registrar reference.</param>
    private void MoveToLiveState(RegisteredDomain domain, DomainStatus status, int sequence)
    {
        domain.Activate($"DEMO-{100000 + sequence}", domain.ExpiresAt, _today);
        switch (status)
        {
            case DomainStatus.Expired:
                domain.MarkExpired(_today);
                break;
            case DomainStatus.Redemption:
                domain.MarkExpired(_today);
                domain.MarkRedemption();
                break;
            case DomainStatus.Transferred:
                domain.MarkTransferred();
                break;
            default:
                break;
        }
    }

    /// <summary>Adds the DNS records every managed zone has and, for every third domain, email forwarding.</summary>
    /// <param name="domain">The active domain.</param>
    /// <param name="name">The domain name, the zone's apex.</param>
    /// <param name="sequence">The domain's running number, used to vary addresses and the optional records.</param>
    private static void AddZone(RegisteredDomain domain, string name, int sequence)
    {
        domain.SetDnsManagement(true);
        domain.AddDnsRecord(DnsRecordType.A, "@", AddressPrefix + (10 + (sequence % 200)), 3600, null);
        domain.AddDnsRecord(DnsRecordType.CNAME, "www", name, 3600, null);
        domain.AddDnsRecord(DnsRecordType.MX, "@", $"mail.{name}", 3600, 10);
        domain.AddDnsRecord(DnsRecordType.TXT, "@", "v=spf1 include:_spf.demo-dns.example ~all", 3600, null);

        if (sequence % 2 == 0)
        {
            domain.AddDnsRecord(DnsRecordType.AAAA, "@", $"2001:db8::{10 + (sequence % 200):x}", 3600, null);
            domain.AddDnsRecord(DnsRecordType.TXT, "_dmarc", "v=DMARC1; p=none", 3600, null);
        }

        if (sequence % 3 == 0)
        {
            domain.SetEmailForwarding(true);
            domain.AddForwardingRule("info", $"owner@{name}");
            domain.AddForwardingRule("sales", $"team@{name}");
        }
    }

    /// <summary>Records the expiry reminders already sent to a domain close to its expiry.</summary>
    /// <param name="domain">The active domain.</param>
    /// <param name="name">The domain name, used for the recipient.</param>
    /// <param name="expiresInDays">Days left until expiry.</param>
    private void AddReminders(RegisteredDomain domain, string name, int expiresInDays)
    {
        if (expiresInDays > ReminderWindowDays)
        {
            return;
        }

        domain.AddReminder(DomainReminderType.ThirtyDaysBeforeExpiry, $"admin@{name}", _today);
        if (expiresInDays <= 14)
        {
            domain.AddReminder(DomainReminderType.FourteenDaysBeforeExpiry, $"admin@{name}", _today);
        }
    }
}
