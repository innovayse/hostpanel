using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Faqs;
using Innovayse.Hostpanel.Domain.Hosting;
using Innovayse.Hostpanel.Domain.Locations;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Partners;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Projects;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssl;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Testimonials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The repository. Handlers write LINQ against these sets and save through
/// <see cref="SaveChangesAsync"/>; there is no repository layer on top.
/// Implemented by the Infrastructure <c>AppDbContext</c>.
/// </summary>
public interface IHostpanelDbContext
{
    /// <summary>Gets the activity log entries table.</summary>
    public DbSet<ActivityLog> ActivityLogs { get; }

    /// <summary>
    /// Gets the roles held by each subject. Separate from Identity's own role tables
    /// because it is keyed by whatever the configured identity provider calls a person,
    /// which in single sign-on mode is not a row in <c>AspNetUsers</c>.
    ///
    /// <para>
    /// Named for the subject rather than the user because <c>IdentityDbContext</c> already
    /// has a <c>UserRoles</c> of its own, holding something else entirely. Two properties
    /// of that name on one context would compile, with <c>new</c>, and mislead every
    /// reader after.
    /// </para>
    /// </summary>
    public DbSet<SubjectRole> SubjectRoles { get; }

    /// <summary>Gets the clients table.</summary>
    public DbSet<Client> Clients { get; }

    /// <summary>Gets the contacts table.</summary>
    public DbSet<Contact> Contacts { get; }

    /// <summary>Gets the product groups table.</summary>
    public DbSet<ProductGroup> ProductGroups { get; }

    /// <summary>Gets the products table.</summary>
    public DbSet<Product> Products { get; }

    /// <summary>Gets the product specification lines table.</summary>
    public DbSet<ProductFeature> ProductFeatures { get; }

    /// <summary>Gets the per-currency product prices table.</summary>
    public DbSet<ProductPrice> ProductPrices { get; }

    /// <summary>Gets the currencies table: one base row plus every rated currency.</summary>
    public DbSet<Currency> Currencies { get; }

    /// <summary>Gets the client services table.</summary>
    public DbSet<ClientService> ClientServices { get; }

    /// <summary>Gets the cancellation requests table.</summary>
    public DbSet<CancellationRequest> CancellationRequests { get; }

    /// <summary>Gets the domains table.</summary>
    public DbSet<RegisteredDomain> Domains { get; }

    /// <summary>Gets the domain renewal reminder emails table.</summary>
    public DbSet<DomainReminder> DomainReminders { get; }

    /// <summary>Gets the billable items table.</summary>
    public DbSet<BillableItem> BillableItems { get; }

    /// <summary>Gets the invoices table.</summary>
    public DbSet<Invoice> Invoices { get; }

    /// <summary>Gets the invoice items table.</summary>
    public DbSet<InvoiceItem> InvoiceItems { get; }

    /// <summary>Gets the sent renewal reminders.</summary>
    public DbSet<InvoiceReminder> InvoiceReminders { get; }

    /// <summary>Gets the fulfilment records: one plugin told one thing about one invoice, and how far it got.</summary>
    public DbSet<FulfilmentRecord> FulfilmentRecords { get; }

    /// <summary>Gets the transactions table.</summary>
    public DbSet<Transaction> Transactions { get; }

    /// <summary>Gets the invoice transactions table.</summary>
    public DbSet<InvoiceTransaction> InvoiceTransactions { get; }

    /// <summary>Gets the quotes table.</summary>
    public DbSet<Quote> Quotes { get; }

    /// <summary>Gets the quote items table.</summary>
    public DbSet<QuoteItem> QuoteItems { get; }

    /// <summary>Gets the orders table.</summary>
    public DbSet<Order> Orders { get; }

    /// <summary>Gets the order items table.</summary>
    public DbSet<OrderItem> OrderItems { get; }

    /// <summary>Gets the support tickets table.</summary>
    public DbSet<Ticket> Tickets { get; }

    /// <summary>Gets the support departments table.</summary>
    public DbSet<Department> Departments { get; }

    /// <summary>Gets the knowledge base articles table.</summary>
    public DbSet<KbArticle> KbArticles { get; }

    /// <summary>Gets the network issues table.</summary>
    public DbSet<NetworkIssue> NetworkIssues { get; }

    /// <summary>Gets the predefined reply categories table.</summary>
    public DbSet<PredefinedReplyCategory> PredefinedReplyCategories { get; }

    /// <summary>Gets the predefined replies table.</summary>
    public DbSet<PredefinedReply> PredefinedReplies { get; }

    /// <summary>Gets the knowledge base categories table.</summary>
    public DbSet<KbCategory> KbCategories { get; }

    /// <summary>Gets the announcements table.</summary>
    public DbSet<Announcement> Announcements { get; }

    /// <summary>Gets the email templates table.</summary>
    public DbSet<EmailTemplate> EmailTemplates { get; }

    /// <summary>Gets the email logs table.</summary>
    public DbSet<EmailLog> EmailLogs { get; }

    /// <summary>Gets the system configuration settings table.</summary>
    public DbSet<Setting> Settings { get; }

    /// <summary>Client-user permission links.</summary>
    public DbSet<ClientUser> ClientUsers { get; }

    /// <summary>Gets the user invitations table.</summary>
    public DbSet<Invitation> Invitations { get; }

    /// <summary>Gets the provisioning servers table.</summary>
    public DbSet<Server> Servers { get; }

    /// <summary>Gets the server groups table.</summary>
    public DbSet<ServerGroup> ServerGroups { get; }

    /// <summary>Gets the SSH keys customers added to their hosting services.</summary>
    public DbSet<SshKey> SshKeys { get; }

    /// <summary>Gets the per-server SSH key management settings.</summary>
    public DbSet<ServerSshSettings> ServerSshSettings { get; }

    /// <summary>Gets the download categories table.</summary>
    public DbSet<DownloadCategory> DownloadCategories { get; }

    /// <summary>Gets the downloads table.</summary>
    public DbSet<Download> Downloads { get; }

    /// <summary>SSL certificate check results.</summary>
    public DbSet<SslCheck> SslChecks { get; }

    /// <summary>Disk and bandwidth usage stats cached from hosting servers.</summary>
    public DbSet<DiskUsageStat> DiskUsageStats { get; }

    /// <summary>Gets the homepage slides table.</summary>
    public DbSet<Slide> Slides { get; }

    /// <summary>Gets the migration jobs table.</summary>
    public DbSet<MigrationJob> MigrationJobs { get; }

    /// <summary>Gets the migration log entries table.</summary>
    public DbSet<MigrationLog> MigrationLogs { get; }

    /// <summary>Gets the TLD configuration and pricing table.</summary>
    public DbSet<TldConfig> TldConfigs { get; }

    /// <summary>Gets the slide per-locale translations table.</summary>
    public DbSet<SlideTranslation> SlideTranslations { get; }

    /// <summary>Gets the storefront client testimonials table.</summary>
    public DbSet<Testimonial> Testimonials { get; }

    /// <summary>Gets the per-locale translations of the storefront client testimonials.</summary>
    public DbSet<TestimonialTranslation> TestimonialTranslations { get; }

    /// <summary>Gets the storefront partners table.</summary>
    public DbSet<Partner> Partners { get; }

    /// <summary>Gets the per-locale translations of the storefront partners.</summary>
    public DbSet<PartnerTranslation> PartnerTranslations { get; }

    /// <summary>Gets the storefront server locations table.</summary>
    public DbSet<ServerLocation> ServerLocations { get; }

    /// <summary>Gets the per-locale translations of the storefront server locations.</summary>
    public DbSet<ServerLocationTranslation> ServerLocationTranslations { get; }

    /// <summary>Gets the storefront portfolio projects table.</summary>
    public DbSet<Project> Projects { get; }

    /// <summary>Gets the per-locale translations of the storefront portfolio projects.</summary>
    public DbSet<ProjectTranslation> ProjectTranslations { get; }

    /// <summary>Gets the storefront frequently asked questions table.</summary>
    public DbSet<Faq> Faqs { get; }

    /// <summary>Gets the per-locale translations of the storefront frequently asked questions.</summary>
    public DbSet<FaqTranslation> FaqTranslations { get; }

    /// <summary>Saves every tracked change in one transaction.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of rows written.</returns>
    public Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Starts an explicit transaction, for the few writes that must commit together across more than
    /// one save (a row whose generated id another row then records). Domain events saved inside it are
    /// published only when it commits and dropped when it rolls back.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The open transaction; disposing it without a commit rolls it back.</returns>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);

    /// <summary>Detaches every tracked entity, clearing pending or failed change state before a fresh save.</summary>
    public void DetachAll();

    /// <summary>
    /// Throws away every change not yet saved: added entities are detached, modified and deleted
    /// ones return to their last loaded values. Entities already saved stay tracked, so one failed
    /// write does not poison the next save of the same unit of work.
    /// </summary>
    public void DiscardPendingChanges();

    /// <summary>
    /// Overwrites a tracked entity with what the database holds now and marks it unchanged, so a
    /// change that lost a concurrency conflict is not retried by a later save.
    /// </summary>
    /// <param name="entity">The tracked entity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public Task ReloadAsync(object entity, CancellationToken ct);
}
