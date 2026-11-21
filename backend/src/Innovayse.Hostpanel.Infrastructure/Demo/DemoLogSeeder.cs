using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the two logs the admin panel lists: the activity log (what staff and the system did to each customer
/// account) and the email log (what was sent, with a few failures). Each line is dated through the entity's backdating
/// method, counted from the anchor and never earlier than the customer's signup, so the logs read as history; the
/// counts, the text and the offsets are the same every reset.
/// </summary>
internal sealed class DemoLogSeeder
{
    /// <summary>How many activity log entries are seeded.</summary>
    internal const int ActivityCount = 160;

    /// <summary>How many email log entries are seeded.</summary>
    internal const int EmailCount = 120;

    /// <summary>Every n-th email is a failed send.</summary>
    private const int FailureEvery = 9;

    /// <summary>The staff who appear in the activity log: id, name and email.</summary>
    private static readonly DemoStaffMember[] Staff =
    [
        new("demo-staff-1", "Demo Admin", "demo-admin@demo.example"),
        new("demo-staff-2", "Rafael Torres", "support.lead@demo.example"),
        new("demo-staff-3", "Julia Weiss", "billing.manager@demo.example"),
    ];

    /// <summary>What happened on an account, rotated.</summary>
    private static readonly string[] Activities =
    [
        "Client profile updated",
        "Invoice created",
        "Payment recorded",
        "Service activated",
        "Service suspended for non-payment",
        "Domain renewal processed",
        "Support ticket answered",
        "Credit added to the account",
        "Login from a new device",
        "Quote delivered to the client",
        "Order accepted",
        "Notification settings changed",
    ];

    /// <summary>The emails that were sent: subject and body, rotated.</summary>
    private static readonly DemoLoggedEmail[] Emails =
    [
        new($"Welcome to {DemoPortalSettingsSeeder.SiteName}", "Your account is ready. Sign in to manage your services."),
        new("Your invoice is ready", "A new invoice has been issued for your account. You can pay it from the billing area."),
        new("Payment received", "Thank you. We have received your payment and applied it to your invoice."),
        new("Your domain expires soon", "A domain on your account expires in less than thirty days. Renew it to keep it."),
        new("Re: your support ticket", "We have replied to your ticket. Open the support area to read the answer."),
        new("Your service is now active", "The service you ordered has been set up and is ready to use."),
    ];

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoLogSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>A moment some days before the anchor, pushed forward to the day after the customer signed up if it falls earlier.</summary>
    /// <param name="client">The customer the line is about.</param>
    /// <param name="daysAgo">How many days before the anchor the line was written.</param>
    /// <param name="hour">The hour of that day.</param>
    /// <returns>The moment to stamp on the line.</returns>
    private DateTimeOffset MomentFor(Domain.Clients.Client client, int daysAgo, int hour)
    {
        var moment = _today.AddDays(-daysAgo).AddHours(hour);
        return DemoAnchor.Clamp(moment, client.CreatedAt.AddDays(1), _today);
    }

    /// <summary>Seeds the logs that are empty.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync(ct);
        if (clients.Count == 0)
        {
            return;
        }

        if (!await _db.ActivityLogs.AnyAsync(ct))
        {
            for (var n = 0; n < ActivityCount; n++)
            {
                var client = clients[n % clients.Count];
                var (id, name, email) = Staff[n % Staff.Length];
                var entry = ActivityLog.Create(
                    client.Id, Activities[n % Activities.Length], id, name, email, $"203.0.113.{20 + (n % 60)}");
                entry.Backdate(MomentFor(client, 1 + ((n * 7) % 150), 8 + (n % 10)));
                _db.ActivityLogs.Add(entry);
            }
        }

        if (!await _db.EmailLogs.AnyAsync(ct))
        {
            // The client area and the admin's client tab find a customer's mail by the account's sign-in address,
            // so each log line is addressed to that address; a made-up one would leave both lists empty.
            var userIds = clients.Select(c => c.UserId).ToList();
            var addresses = await _db.Users
                .Where(u => userIds.Contains(u.Id) && u.Email != null)
                .ToDictionaryAsync(u => u.Id, u => u.Email!, ct);

            for (var n = 0; n < EmailCount; n++)
            {
                var client = clients[n % clients.Count];
                var (subject, body) = Emails[n % Emails.Length];
                var failed = n % FailureEvery == FailureEvery - 1;
                var to = addresses.TryGetValue(client.UserId, out var address)
                    ? address
                    : $"{DemoSites.LabelOf(client)}@demo.example";
                var sent = EmailLog.Create(
                    to, subject, body,
                    failed ? EmailSendFailure.RecipientRejected : null,
                    _today);
                sent.Backdate(MomentFor(client, 1 + ((n * 5) % 110), 7 + (n % 12)));
                _db.EmailLogs.Add(sent);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
