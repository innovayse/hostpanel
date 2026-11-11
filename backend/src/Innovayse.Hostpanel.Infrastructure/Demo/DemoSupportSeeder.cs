using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the support side: an extra department, predefined reply categories with canned replies, a knowledge
/// base of categories and articles, sixty-six tickets in every status the entity can reach with multi-message
/// threads, tags, flags and feedback, twenty-five announcements and the network issues the status page lists. The
/// default departments are seeded for every deployment elsewhere; this seeder only adds to and files into them.
/// </summary>
internal sealed class DemoSupportSeeder
{
    /// <summary>How many tickets are seeded.</summary>
    private const int TicketCount = 66;

    /// <summary>How many of the first tickets belong to the published demo client, so its portal shows open, answered and closed threads.</summary>
    private const int DemoClientTickets = 6;

    /// <summary>The name of the department the demo adds to the default ones.</summary>
    private const string ExtraDepartment = "Abuse and Security";

    /// <summary>The name staff replies are signed with.</summary>
    private const string AgentName = "Support Team";

    /// <summary>The name of the default department that handles technical problems.</summary>
    private const string TechnicalDepartment = "Technical Support";

    /// <summary>The name of the default department that handles invoices and payments.</summary>
    private const string BillingDepartment = "Billing";

    /// <summary>The name of the default department that handles plans and quotes.</summary>
    private const string SalesDepartment = "Sales";

    /// <summary>The name of the default department that handles everything else.</summary>
    private const string GeneralDepartment = "General";

    /// <summary>The tags that fit the tickets of each department, rotated; a ticket is only ever tagged from its own department's list.</summary>
    private static readonly Dictionary<string, string[]> TagsByDepartment = new()
    {
        [TechnicalDepartment] = ["dns", "ssl", "email", "vps", "urgent"],
        [BillingDepartment] = ["billing", "refund"],
        [SalesDepartment] = ["migration", "vps"],
        [GeneralDepartment] = ["email", "migration"],
        [ExtraDepartment] = ["urgent", "ssl"],
    };

    /// <summary>The subjects, opening messages and handling departments of the tickets, in order and repeating past the end; the first ones belong to the demo client.</summary>
    private static readonly DemoTicketTopic[] TicketTopics =
    [
        new("My site is loading slowly since yesterday", "The pages of my site have taken several seconds to load since yesterday evening. I have not changed anything on my side. Could you take a look?", TechnicalDepartment),
        new("Question about the invoice for last month", "Could you tell me why the invoice for last month includes a domain renewal line?", BillingDepartment),
        new("Please add an extra mailbox to my plan", "We hired a new colleague and need one more mailbox on our domain. What is the quickest way to add it?", SalesDepartment),
        new("Need help pointing my domain to an external service", "I need to add a CNAME record for a third-party service and I am not sure which values to use.", TechnicalDepartment),
        new("Request to upgrade to a VPS", "I would like to move from shared hosting to a VPS. What are the steps and the new monthly price?", SalesDepartment),
        new("Certificate warning on my site", "Visitors see a security warning on my site since this morning. Please check the certificate.", TechnicalDepartment),
        new("Email is bouncing for one recipient", "Messages to one customer come back as undeliverable. Other recipients are fine.", TechnicalDepartment),
        new("Restore a file deleted by mistake", "I removed a folder from my account this morning and would like it restored from last night's backup.", TechnicalDepartment),
        new("Transfer a domain from another provider", "I started a transfer yesterday and have not received any confirmation. Can you check the status?", GeneralDepartment),
        new("Charged twice for the same service", "My card statement shows two charges for the same hosting renewal. Please look into it.", BillingDepartment),
        new("How do I enable two-factor sign-in?", "I cannot find the setting for two-factor sign-in in my account. Where is it?", GeneralDepartment),
        new("Server unreachable from my office", "My dedicated server answers from home but not from the office network. Could a firewall rule be blocking us?", TechnicalDepartment),
        new("Change the billing contact", "Our accountant has changed. Please send invoices to the new address from now on.", BillingDepartment),
        new("Increase the disk quota", "My account is close to its disk limit. What are the options for more space?", SalesDepartment),
        new("Reverse DNS for my mail server", "Our outgoing mail is flagged because the reverse DNS does not match. Can you set it for our address?", TechnicalDepartment),
        new("Website shows a database error", "Since an update this afternoon the site shows a database connection error.", TechnicalDepartment),
        new("Cancel auto-renew for an old domain", "We no longer need one of our domains. Please turn off auto-renew for it.", GeneralDepartment),
        new("Quote for a managed migration", "We plan to move twelve sites from another host. Could you quote a managed migration?", SalesDepartment),
        new("Abuse report received about my account", "I received a notice that a site on my account sent spam. I believe it was hacked. What should I do?", ExtraDepartment),
        new("Slow response from the control panel", "The control panel takes a long time to open in the evening. Is there a known issue?", TechnicalDepartment),
        new("Add a staging copy of my site", "We would like a staging copy of our shop to test changes before they go live. Is that possible on our plan?", TechnicalDepartment),
        new("Cron job does not run", "A scheduled task I set up last week has never run. The command works when I start it by hand.", TechnicalDepartment),
        new("Need a copy of a paid invoice", "Our auditor asks for a copy of an invoice we paid in spring. Where can I download it?", BillingDepartment),
        new("Move my VPS to another region", "Most of our visitors are now in North America. Can the VPS be moved closer to them?", SalesDepartment),
        new("Upload limit too small", "Uploading a large video to the site fails. Can the upload size limit be raised?", TechnicalDepartment),
        new("Domain shows as expired", "One of our domains shows as expired although auto-renew was on. What happened?", GeneralDepartment),
        new("Set up mail on a new phone", "A colleague needs the mailbox on a new phone. Which server settings should she use?", TechnicalDepartment),
        new("Too many failed sign-in alerts", "We get alerts about failed sign-ins on our account every night. Is someone trying to break in?", ExtraDepartment),
        new("Change the primary domain of a service", "We rebranded and want the hosting service to use the new domain as its main one.", GeneralDepartment),
        new("Invoice in the wrong currency", "Our latest invoice is in dollars but we pay in euros. Can it be reissued?", BillingDepartment),
        new("Snapshot before an upgrade", "Please take a snapshot of the VPS before we upgrade the operating system tonight.", TechnicalDepartment),
        new("Website redirects to the wrong page", "After moving to secure connections the home page redirects to an old landing page.", TechnicalDepartment),
        new("Add a second admin to our account", "Our new office manager needs access to billing and tickets. How do we add her?", GeneralDepartment),
        new("Bandwidth warning received", "We got a notice that the service is close to its monthly bandwidth. What happens if we go over?", SalesDepartment),
        new("Unable to connect over SSH", "SSH to the server times out since this morning. The website itself still loads.", TechnicalDepartment),
        new("Refund for an unused add-on", "We ordered a dedicated IP by mistake and never used it. Could it be refunded?", BillingDepartment),
        new("Wildcard certificate installation", "We bought a wildcard certificate and need help installing it on three subdomains.", TechnicalDepartment),
        new("Mailbox is full", "One mailbox stopped receiving mail because it is full. Can it be enlarged or archived?", TechnicalDepartment),
        new("Database import fails", "Importing a database dump stops halfway with a timeout. Is there another way to import it?", TechnicalDepartment),
        new("Question about the reseller plan", "How many client accounts can we create on the reseller plan, and can they have their own branding?", SalesDepartment),
        new("PHP version for an old site", "One of our older sites needs an older PHP version. Can it run side by side with the others?", TechnicalDepartment),
        new("Order stuck in pending", "We placed an order two days ago and it still shows as pending. Is anything missing on our side?", SalesDepartment),
        new("Block traffic from one address", "One address keeps flooding our contact form. Can you block it at the server level?", ExtraDepartment),
        new("Close our old account", "We have moved everything to a new account. Please close the old one once the last invoice is settled.", GeneralDepartment),
    ];

    /// <summary>What staff say in a thread, in turn.</summary>
    private static readonly string[] StaffReplies =
    [
        "Thank you for contacting us. We are looking into this and will get back to you shortly.",
        "We found the cause and have applied a fix on our side. Please check again and tell us what you see.",
        "Our guide in the knowledge base shows the steps. If anything is unclear, reply here and we will walk you through it.",
        "We have made the change you asked for. It can take a few minutes to take effect.",
        "Glad it helps. We will keep an eye on the account for the next few days.",
    ];

    /// <summary>What customers say in a thread, in turn.</summary>
    private static readonly string[] ClientReplies =
    [
        "Thanks, that is much faster now. Is there anything I should change so this does not come back?",
        "I tried that and it works for the main page but not for the second one. Any idea?",
        "Perfect, thank you. That was exactly what I needed.",
    ];

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoSupportSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds the support data that is not there yet.</summary>
    /// <param name="demoClientId">The published demo client, who owns the first sample ticket threads.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(int demoClientId, CancellationToken ct)
    {
        if (!await _db.Departments.AnyAsync(d => d.Name == ExtraDepartment, ct))
        {
            _db.Departments.Add(Department.Create(ExtraDepartment, "abuse@demo.example"));
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.PredefinedReplyCategories.AnyAsync(ct))
        {
            await SeedPredefinedRepliesAsync(ct);
        }

        if (!await _db.KbArticles.AnyAsync(ct))
        {
            await SeedKnowledgeBaseAsync(ct);
        }

        if (!await _db.Tickets.AnyAsync(ct))
        {
            await SeedTicketsAsync(demoClientId, ct);
        }

        if (!await _db.Announcements.AnyAsync(ct))
        {
            await SeedAnnouncementsAsync(ct);
        }

        if (!await _db.NetworkIssues.AnyAsync(ct))
        {
            await SeedNetworkIssuesAsync(ct);
        }
    }

    /// <summary>Seeds four reply categories with three canned replies each.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedPredefinedRepliesAsync(CancellationToken ct)
    {
        (string Category, (string Name, string Content)[] Replies)[] catalogue =
        [
            ("General",
            [
                ("Greeting", "Hello, thank you for contacting us. How can we help you today?"),
                ("More information needed", "Could you send us a few more details, such as the domain name and when the problem started?"),
                ("Closing a resolved ticket", "We are glad we could help. We will close this ticket; reply at any time to reopen it."),
            ]),
            ("Billing",
            [
                ("Invoice explained", "Each invoice lists the service, the billing period and the due date. The line you ask about is the renewal of your service."),
                ("Payment received", "We have received your payment and applied it to the invoice. A receipt is available in your billing area."),
                ("Refund processed", "Your refund has been issued to the original payment method and should arrive within five business days."),
            ]),
            ("Technical",
            [
                ("Clear the cache", "Please clear your browser cache and any caching plugin on your site, then check the page again."),
                ("DNS propagation", "DNS changes can take up to 48 hours to reach every network. Please check again later."),
                ("Restore from backup", "We have restored the requested files from last night's backup. Please confirm that everything is in place."),
            ]),
            ("Sales",
            [
                ("Plan comparison", "Our plans differ in storage, number of sites and support level. We recommend the one that fits your traffic with room to grow."),
                ("Upgrade steps", "You can upgrade from the service page. The new plan starts at once and the difference is billed pro rata."),
                ("Migration offer", "We offer migration assistance for your sites; tell us which ones to move and we will plan it with you."),
            ]),
        ];

        foreach (var (category, replies) in catalogue)
        {
            var created = PredefinedReplyCategory.Create(category);
            _db.PredefinedReplyCategories.Add(created);
            await _db.SaveChangesAsync(ct);

            foreach (var (name, content) in replies)
            {
                _db.PredefinedReplies.Add(PredefinedReply.Create(name, content, created.Id));
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds knowledge base categories and articles, all published but one draft.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedKnowledgeBaseAsync(CancellationToken ct)
    {
        (string Name, string Description)[] categories =
        [
            ("Getting Started", "Guides to help you get up and running quickly."),
            ("Billing & Payments", "Invoices, payment methods and refunds."),
            ("Domains", "Domain registration, DNS management and transfers."),
            ("Hosting", "Shared hosting, VPS and server management."),
            ("Security", "SSL certificates, two-factor sign-in and account safety."),
            ("Email", "Mailboxes, forwarding and deliverability."),
            ("Troubleshooting", "Fixes for the problems customers meet most often."),
        ];

        foreach (var (name, description) in categories)
        {
            _db.KbCategories.Add(KbCategory.Create(name, SlugGenerator.FromText(name, KbSlugAllocator.CategoryFallback, KbSlugRules.MaxLength), description, false));
        }

        await _db.SaveChangesAsync(ct);
        var categoryIds = await _db.KbCategories.AsNoTracking().ToDictionaryAsync(c => c.Name, c => c.Id, ct);

        (string Title, string Content, string Category)[] articles =
        [
            ("How to sign in to the client portal", "Open the client portal, enter your email and password, and press Sign in. If two-factor sign-in is on, you will also be asked for the six-digit code from your authenticator app.", "Getting Started"),
            ("How to turn on two-factor sign-in", "Open Account, find the Security section and choose Enable. Scan the QR code with an authenticator app, then type the six-digit code to confirm.", "Getting Started"),
            ("Your first hour with a new service", "After an order is accepted the service is set up within minutes. Open the service page to see the sign-in details and the next steps.", "Getting Started"),
            ("How to pay an invoice", "Open Billing, pick the invoice and press Pay now. You can pay by card or follow the bank transfer instructions shown on the invoice.", "Billing & Payments"),
            ("Understanding your invoice", "Each invoice lists what you are billed for, the due date and the total. An invoice marked Overdue has passed its due date without payment.", "Billing & Payments"),
            ("How credit works", "Credit on your account is used automatically against your next invoice. You can add credit from the Billing page at any time.", "Billing & Payments"),
            ("How to register a domain", "Open Domains and press Register. Search for the name you want, add it to the cart and complete the order. Most domains are active within minutes.", "Domains"),
            ("How to update your DNS records", "Open the domain, choose DNS management and use the Add record form for A, CNAME, MX or TXT entries. Changes can take up to 48 hours to spread.", "Domains"),
            ("How to transfer a domain to us", "Unlock the domain at your current provider, copy its authorisation code and start the transfer from Domains. The transfer takes up to seven days.", "Domains"),
            ("Choosing a hosting plan", "Starter Hosting suits a single small site. Business Hosting suits growing businesses. Pro Hosting is for busy sites and agencies running many projects.", "Hosting"),
            ("How to upgrade your hosting plan", "Open the service and press Upgrade, or ask support through a ticket. Upgrades are applied within one business day and billed pro rata.", "Hosting"),
            ("Moving from shared hosting to a VPS", "A VPS gives you dedicated resources and full control. Order the plan, tell support which sites to move, and we migrate them for you.", "Hosting"),
            ("How an SSL certificate protects your site", "A certificate encrypts the connection between your visitors and your server, protects what they type and builds trust. Every hosting plan includes a free certificate.", "Security"),
            ("Keeping your account secure", "Use a strong, unique password and turn on two-factor sign-in. Never share your credentials. If you suspect misuse, change your password at once and contact support.", "Security"),
            ("How to set up a mailbox", "Open the email service, choose Add mailbox, pick a name and password, and use the settings shown to connect your mail app.", "Email"),
            ("Forwarding mail to another address", "Open the domain, choose Email forwarding and add a rule from the address you want to the address that should receive the mail.", "Email"),
            ("My site shows an error page", "Clear your cache first, then check that the domain points to the right server. If the error persists, send us the exact message and the time you saw it.", "Troubleshooting"),
            ("Logging in to the control panel from the client area", "Open the service, then press Log in to control panel. You are signed in without typing a separate password.", "Getting Started"),
            ("Ordering more than one service at once", "Add each service to the cart before checking out. One invoice covers the whole order.", "Getting Started"),
            ("Changing your billing currency", "You can change the billing currency from your account until the first invoice is issued. After that, ask support.", "Billing & Payments"),
            ("Saving a card for automatic payments", "Open Billing, choose Payment methods and add a card. Renewals are then charged on the due date.", "Billing & Payments"),
            ("Downloading invoices for your records", "Every invoice in Billing has a Download button that saves a PDF copy for your accountant.", "Billing & Payments"),
            ("Keeping a domain from expiring", "Turn on auto-renew and keep a valid payment method on file. Reminders are sent thirty and fourteen days before expiry.", "Domains"),
            ("What the registrar lock does", "The lock stops anyone from transferring the domain away without your consent. Turn it off only while a transfer is in progress.", "Domains"),
            ("Scheduling tasks with cron", "Open the control panel, choose Scheduled tasks and add the command and the times it should run. Output can be mailed to you.", "Hosting"),
            ("Taking and restoring VPS snapshots", "Open the VPS, choose Snapshots and press Create. Restoring a snapshot returns the server to that moment.", "Hosting"),
            ("Installing a certificate on a subdomain", "Open SSL, choose the certificate and add the subdomain. The certificate is issued and installed within minutes.", "Security"),
            ("Recognising phishing messages", "We never ask for your password by email. Check the sender and the link address before you sign in anywhere.", "Security"),
            ("Improving email deliverability", "Publish SPF, DKIM and DMARC records for your domain so receiving servers trust the mail you send.", "Email"),
            ("Mailbox storage is full", "Delete or archive old messages, or upgrade the mail plan for more space. Incoming mail is held for a short time meanwhile.", "Email"),
            ("The site is slow after an update", "Disable recently added plugins one by one, clear the cache and check the error log in the control panel.", "Troubleshooting"),
            ("I cannot connect over SSH", "Check that your address is not blocked after failed attempts and that you use the right port and key. Support can unblock you.", "Troubleshooting"),
            ("Draft: planned maintenance checklist", "This article is being written and is not yet visible to customers.", "Troubleshooting"),
        ];

        foreach (var (title, content, category) in articles)
        {
            var article = KbArticle.Create(categoryIds[category], SlugGenerator.FromText(title, KbSlugAllocator.ArticleFallback, KbSlugRules.MaxLength));
            article.SetTranslation(SupportedLanguages.Default, title, content);
            if (!title.StartsWith("Draft:", StringComparison.Ordinal))
            {
                article.Publish();
            }

            _db.KbArticles.Add(article);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds tickets: the first few on the demo client, the rest spread over the other customers. Each follows one of
    /// five thread shapes (unanswered, answered, a back-and-forth, resolved with feedback, closed after one answer).
    /// </summary>
    /// <param name="demoClientId">The published demo client.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedTicketsAsync(int demoClientId, CancellationToken ct)
    {
        var departments = await _db.Departments.OrderBy(d => d.Id).ToListAsync(ct);
        var demoClient = await _db.Clients.FirstOrDefaultAsync(c => c.Id == demoClientId, ct);
        var otherClients = await _db.Clients.Where(c => c.Id != demoClientId).OrderBy(c => c.Id).ToListAsync(ct);
        if (departments.Count == 0 || otherClients.Count == 0 || demoClient is null)
        {
            return;
        }

        var priorities = new[] { TicketPriority.High, TicketPriority.Medium, TicketPriority.Low };
        for (var n = 0; n < TicketCount; n++)
        {
            var (subject, message, departmentName) = TicketTopics[n % TicketTopics.Length];
            var owner = n < DemoClientTickets ? demoClient : otherClients[(n - DemoClientTickets) % otherClients.Count];

            // A ticket goes to the department that handles its subject; a deployment whose departments were renamed falls back to the first one.
            var department = departments.FirstOrDefault(d => d.Name == departmentName) ?? departments[0];
            var ticket = Ticket.Create(owner.Id, subject, message, department.Id, priorities[n % priorities.Length], _today);
            var customer = $"{owner.FirstName} {owner.LastName}".Trim();

            var shape = n % 5;
            if (shape >= 1)
            {
                ticket.AddReply(StaffReplies[n % StaffReplies.Length], AgentName, true, _today);
            }

            if (shape is 2 or 3)
            {
                ticket.AddReply(ClientReplies[n % ClientReplies.Length], customer, false, _today);
                ticket.AddReply(StaffReplies[(n + 2) % StaffReplies.Length], AgentName, true, _today);
            }

            if (shape == 2)
            {
                ticket.AddReply(ClientReplies[(n + 1) % ClientReplies.Length], customer, false, _today);
            }

            if (shape is 3 or 4)
            {
                ticket.Close(_today);
                ticket.LeaveFeedback(6 + (n % 5), shape == 3 ? "Quick and helpful, thank you." : null, customer, _today);
            }

            var tags = TagsByDepartment.GetValueOrDefault(department.Name) ?? TagsByDepartment[GeneralDepartment];
            ticket.AddTag(tags[n % tags.Length]);
            if (n % 3 == 0)
            {
                ticket.AddTag(tags[(n + 1) % tags.Length]);
            }

            if (n % 6 == 0)
            {
                ticket.ToggleFlag();
            }

            // The ticket was opened days ago and each message followed hours after the last, not all at the moment of seeding.
            ticket.Backdate(_today.AddDays(-(2 + ((n * 7) % 58))).AddHours(8 + (n % 9)), TimeSpan.FromHours(2 + (n % 4)));
            _db.Tickets.Add(ticket);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds twenty-five announcements, the last of them an unpublished draft.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedAnnouncementsAsync(CancellationToken ct)
    {
        (string Title, string Content, bool Published)[] announcements =
        [
            ("Welcome to the demo", "This is a public demo. Explore the admin panel and the client portal freely; the data is reset on a schedule shown on the sign-in page.", true),
            ("Scheduled maintenance", "We will perform routine maintenance on the shared hosting servers this weekend between 02:00 and 04:00 UTC. Sites may be briefly unavailable.", true),
            ("New: two-factor sign-in", "You can now protect your account with two-factor sign-in. Open Account, then Security, to turn it on in under a minute.", true),
            ("Faster NVMe storage on every VPS", "All virtual servers now run on NVMe storage at no extra cost. Existing servers are moved automatically during the next maintenance window.", true),
            ("Pay in euros", "Customers in supported regions can now see prices and pay invoices in euros. Change your billing currency from your account if you have no invoices yet.", true),
            ("New data centre location", "Our Helsinki data centre is open for dedicated servers. Choose it when you order.", true),
            ("Improved spam protection for email", "Every mailbox now has a stronger spam filter. No action is needed.", true),
            ("Holiday support hours", "Our support team answers with a short delay over the holidays. Urgent issues are still handled around the clock.", true),
            ("Wildcard certificates now issued in minutes", "Wildcard certificates are now validated automatically, so most are issued within minutes of the order.", true),
            ("Snapshots for every VPS", "Every virtual server can now keep up to three snapshots at no extra cost. Open the server to take one.", true),
            ("Price update for dedicated servers", "From next quarter the dedicated server range gets more memory at the same price. Existing servers are upgraded on request.", true),
            ("New knowledge base articles", "We added guides on deliverability, scheduled tasks and SSH access. Find them in the knowledge base.", true),
            ("Reseller branding options", "Reseller accounts can now show their own logo and colours in the control panel their clients see.", true),
            ("Improved status page", "The network status page now lists planned work ahead of time and keeps a history of resolved issues.", true),
            ("Faster support for priority customers", "Customers with the priority support add-on now get a first answer within one hour, day and night.", true),
            ("Annual billing discount", "Paying annually now saves about two months compared with monthly billing on every hosting plan.", true),
            ("IPv6 on every server", "Every shared, virtual and dedicated server now has an IPv6 address alongside IPv4.", true),
            ("Team access for client accounts", "Invite colleagues to your account and choose what each of them can see and do.", true),
            ("Domain search now suggests alternatives", "When a name is taken, the domain search now suggests similar names across popular extensions.", true),
            ("Larger mailboxes at the same price", "Mail Business mailboxes grow from 25 GB to 30 GB each, with no change to the price.", true),
            ("Website monitoring add-on", "Get an alert within a minute when your site stops answering, by mail or text message.", true),
            ("Updated terms of service", "We have clarified the acceptable use and refund sections of our terms. Nothing changes for existing services.", true),
            ("New payment receipts", "Payment receipts now list every invoice line and can be downloaded as PDF from the billing area.", true),
            ("Quarterly network report", "Uptime across all regions stayed above the target this quarter. The full report is on the status page.", true),
            ("Upcoming: new client area", "We are preparing a redesigned client area. This note is a draft and is not yet shown to customers.", false),
        ];

        // The list runs from the oldest to the newest, so the first one was posted about five months ago and the last
        // (the draft) a few days ago; every announcement gets its own day instead of the moment of seeding.
        for (var n = 0; n < announcements.Length; n++)
        {
            var (title, content, published) = announcements[n];
            var announcement = Announcement.Create(title, content, published);
            announcement.Backdate(_today.AddDays(-(3 + ((announcements.Length - 1 - n) * 6))).AddHours(9 + (n % 8)));
            _db.Announcements.Add(announcement);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds twelve network issues in every status: reported, under investigation, scheduled and resolved.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedNetworkIssuesAsync(CancellationToken ct)
    {
        (string Title, NetworkIssueType Type, string? Server, NetworkIssuePriority Priority, NetworkIssueStatus Status, int StartedDaysAgo, string Description, int ResolvedAfterMinutes)[] issues =
        [
            ("Packet loss on the Frankfurt uplink", NetworkIssueType.Server, "Shared EU-01", NetworkIssuePriority.High, NetworkIssueStatus.Investigating, 0, "<p>Some customers see slow loading. Our engineers are checking the upstream link.</p>", 0),
            ("Planned switch replacement", NetworkIssueType.Other, null, NetworkIssuePriority.Medium, NetworkIssueStatus.Scheduled, -3, "<p>We replace a core switch this weekend. Expect up to ten minutes of interruption.</p>", 0),
            ("Control panel login errors", NetworkIssueType.Server, "VPS EU-01", NetworkIssuePriority.Low, NetworkIssueStatus.Reported, 1, "<p>A few users could not sign in to the control panel. We are collecting reports.</p>", 0),
            ("Mail delivery delays", NetworkIssueType.Other, null, NetworkIssuePriority.Medium, NetworkIssueStatus.Resolved, 9, "<p>Outgoing mail was delayed by up to an hour. The queue has been cleared.</p>", 90),
            ("Disk failure on a storage node", NetworkIssueType.Server, "Dedicated EU-01", NetworkIssuePriority.Critical, NetworkIssueStatus.Resolved, 21, "<p>A failed disk was replaced and the array rebuilt. No data was lost.</p>", 420),
            ("DNS resolver slowdown", NetworkIssueType.Other, null, NetworkIssuePriority.High, NetworkIssueStatus.Resolved, 40, "<p>Name resolution was slow for about two hours. A resolver was restarted and monitoring added.</p>", 150),
            ("Backup jobs running late", NetworkIssueType.Server, "Shared EU-02", NetworkIssuePriority.Low, NetworkIssueStatus.Investigating, 1, "<p>Last night's backups finished later than usual. Engineers are checking the storage throughput.</p>", 0),
            ("Kernel updates on virtual servers", NetworkIssueType.Server, "VPS US-01", NetworkIssuePriority.Medium, NetworkIssueStatus.Scheduled, -7, "<p>Host nodes will be patched and rebooted one by one. Each server restarts once, for about five minutes.</p>", 0),
            ("Webmail sign-in slow", NetworkIssueType.Other, null, NetworkIssuePriority.Low, NetworkIssueStatus.Reported, 0, "<p>Some users report that webmail takes long to open. We are gathering details.</p>", 0),
            ("Power maintenance in one rack", NetworkIssueType.Server, "Dedicated US-01", NetworkIssuePriority.High, NetworkIssueStatus.Resolved, 60, "<p>Power supplies were replaced during the announced window. All servers came back as planned.</p>", 240),
            ("Certificate renewals delayed", NetworkIssueType.Other, null, NetworkIssuePriority.Medium, NetworkIssueStatus.Resolved, 75, "<p>Automatic renewals were held back for a few hours. Every certificate was renewed before expiry.</p>", 300),
            ("Brief outage on the billing pages", NetworkIssueType.Other, null, NetworkIssuePriority.Critical, NetworkIssueStatus.Resolved, 95, "<p>The billing pages were unavailable for twelve minutes after a deployment. The change was rolled back.</p>", 12),
        ];

        foreach (var (title, type, server, priority, status, startedDaysAgo, description, resolvedAfterMinutes) in issues)
        {
            var start = _today.AddDays(-startedDaysAgo);
            var issue = NetworkIssue.Create(title, type, server, priority, start, description);
            if (status == NetworkIssueStatus.Resolved)
            {
                // Resolved when the work was done, a few minutes or hours after it began, not at the moment of seeding.
                issue.Update(title, type, server, priority, status, start, start.AddMinutes(resolvedAfterMinutes), description);
            }
            else if (status != NetworkIssueStatus.Reported)
            {
                issue.Update(title, type, server, priority, status, start, null, description);
            }

            _db.NetworkIssues.Add(issue);
        }

        await _db.SaveChangesAsync(ct);
    }
}
