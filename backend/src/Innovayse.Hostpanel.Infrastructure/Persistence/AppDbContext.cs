using Innovayse.Hostpanel.Application.Common.Interfaces;
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
using Innovayse.Hostpanel.Domain.Scheduling;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssl;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Testimonials;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// Root EF Core DbContext for the Hostpanel backend.
/// Extends <see cref="IdentityDbContext{TUser}"/> to include ASP.NET Core Identity tables.
/// All entity configurations are discovered automatically via <see cref="OnModelCreating"/>.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<AppUser>, IHostpanelDbContext, IDataProtectionKeyContext
{
    /// <summary>The service the model's protected columns are encrypted with.</summary>
    private readonly IEncryptionService _encryption;

    /// <summary>Initializes a new instance of the <see cref="AppDbContext"/> class.</summary>
    /// <param name="options">The provider and connection the context runs on, configured at registration.</param>
    /// <param name="encryption">
    /// The service that encrypts the <see cref="ProtectedColumns"/>. Required: a context cannot
    /// exist without it, so no path (design time, tests, a second host) can build a model that
    /// silently stores credentials in plain text.
    /// </param>
    /// <remarks>
    /// EF caches the built model per context type, so the converter in it is bound to the service
    /// of the first context built in the process. The key never changes while a process runs, so
    /// every context agrees with it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encryption"/> is null.</exception>
    public AppDbContext(DbContextOptions<AppDbContext> options, IEncryptionService encryption)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(encryption);
        _encryption = encryption;
    }

    /// <summary>Gets the activity log entries table.</summary>
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

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
    public DbSet<SubjectRole> SubjectRoles => Set<SubjectRole>();

    /// <summary>Gets the clients table.</summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>Gets the contacts table.</summary>
    public DbSet<Contact> Contacts => Set<Contact>();

    /// <summary>Gets the product groups table.</summary>
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

    /// <summary>Gets the products table.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Gets the product specification lines table.</summary>
    public DbSet<ProductFeature> ProductFeatures => Set<ProductFeature>();

    /// <summary>Gets the per-currency product prices table.</summary>
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    /// <summary>Gets the currencies table: one base row plus every rated currency.</summary>
    public DbSet<Domain.Billing.Currency> Currencies => Set<Domain.Billing.Currency>();

    /// <summary>Gets the client services table.</summary>
    public DbSet<ClientService> ClientServices => Set<ClientService>();

    /// <summary>Gets the cancellation requests table.</summary>
    public DbSet<CancellationRequest> CancellationRequests => Set<CancellationRequest>();

    /// <summary>Gets the domains table.</summary>
    public DbSet<RegisteredDomain> Domains => Set<RegisteredDomain>();

    /// <summary>Gets the domain renewal reminder emails table.</summary>
    public DbSet<DomainReminder> DomainReminders => Set<DomainReminder>();

    /// <summary>
    /// Gets the DNS records table. Records are reached through their domain; the set exists so
    /// the table takes its PascalCase name from this property like every other table here.
    /// </summary>
    public DbSet<DnsRecord> DnsRecords => Set<DnsRecord>();

    /// <summary>Gets the domain nameservers table, named by this property like every other table.</summary>
    public DbSet<Nameserver> Nameservers => Set<Nameserver>();

    /// <summary>Gets the email forwarding rules table, named by this property like every other table.</summary>
    public DbSet<EmailForwardingRule> EmailForwardingRules => Set<EmailForwardingRule>();

    /// <summary>Gets the billable items table.</summary>
    public DbSet<BillableItem> BillableItems => Set<BillableItem>();

    /// <summary>Gets the invoices table.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Gets the invoice items table.</summary>
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    /// <summary>Gets the sent renewal reminders.</summary>
    public DbSet<InvoiceReminder> InvoiceReminders => Set<InvoiceReminder>();

    /// <summary>Gets the fulfilment records: one plugin told one thing about one invoice, and how far it got.</summary>
    public DbSet<FulfilmentRecord> FulfilmentRecords => Set<FulfilmentRecord>();

    /// <summary>Gets the transactions table.</summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>Gets the invoice transactions table.</summary>
    public DbSet<InvoiceTransaction> InvoiceTransactions => Set<InvoiceTransaction>();

    /// <summary>Gets the quotes table.</summary>
    public DbSet<Quote> Quotes => Set<Quote>();

    /// <summary>Gets the quote items table.</summary>
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();

    /// <summary>Gets the orders table.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Gets the order items table.</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>Gets the support tickets table.</summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>Gets the ticket replies table, named by this property like every other table.</summary>
    public DbSet<TicketReply> TicketReplies => Set<TicketReply>();

    /// <summary>Gets the ticket tags table, named by this property like every other table.</summary>
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();

    /// <summary>Gets the support departments table.</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>Gets the knowledge base articles table.</summary>
    public DbSet<KbArticle> KbArticles => Set<KbArticle>();

    /// <summary>Gets the network issues table.</summary>
    public DbSet<NetworkIssue> NetworkIssues => Set<NetworkIssue>();

    /// <summary>Gets the predefined reply categories table.</summary>
    public DbSet<PredefinedReplyCategory> PredefinedReplyCategories => Set<PredefinedReplyCategory>();

    /// <summary>Gets the predefined replies table.</summary>
    public DbSet<PredefinedReply> PredefinedReplies => Set<PredefinedReply>();

    /// <summary>Gets the knowledge base categories table.</summary>
    public DbSet<KbCategory> KbCategories => Set<KbCategory>();

    /// <summary>Gets the announcements table.</summary>
    public DbSet<Announcement> Announcements => Set<Announcement>();

    /// <summary>Gets the email templates table.</summary>
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    /// <summary>Gets the email logs table.</summary>
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    /// <summary>Gets the system configuration settings table.</summary>
    public DbSet<Setting> Settings => Set<Setting>();

    /// <summary>Client-user permission links.</summary>
    public DbSet<ClientUser> ClientUsers => Set<ClientUser>();

    /// <summary>Gets the user invitations table.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Gets the provisioning servers table.</summary>
    public DbSet<Server> Servers => Set<Server>();

    /// <summary>Gets the server groups table.</summary>
    public DbSet<ServerGroup> ServerGroups => Set<ServerGroup>();

    /// <summary>Gets the SSH keys customers added to their hosting services.</summary>
    public DbSet<SshKey> SshKeys => Set<SshKey>();

    /// <summary>Gets the per-server SSH key management settings.</summary>
    public DbSet<ServerSshSettings> ServerSshSettings => Set<ServerSshSettings>();

    /// <summary>Gets the download categories table.</summary>
    public DbSet<DownloadCategory> DownloadCategories => Set<DownloadCategory>();

    /// <summary>Gets the downloads table.</summary>
    public DbSet<Download> Downloads => Set<Download>();

    /// <summary>SSL certificate check results.</summary>
    public DbSet<SslCheck> SslChecks => Set<SslCheck>();

    /// <summary>Disk and bandwidth usage stats cached from hosting servers.</summary>
    public DbSet<DiskUsageStat> DiskUsageStats => Set<DiskUsageStat>();

    /// <summary>Gets the homepage slides table.</summary>
    public DbSet<Slide> Slides => Set<Slide>();

    /// <summary>Gets the recurring jobs' run records (lease and last run), shared by every instance.</summary>
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    /// <summary>Gets the migration jobs table.</summary>
    public DbSet<MigrationJob> MigrationJobs => Set<MigrationJob>();

    /// <summary>Gets the migration log entries table.</summary>
    public DbSet<MigrationLog> MigrationLogs => Set<MigrationLog>();

    /// <summary>Gets the TLD configuration and pricing table.</summary>
    public DbSet<TldConfig> TldConfigs => Set<TldConfig>();

    /// <summary>Gets the slide per-locale translations table.</summary>
    public DbSet<SlideTranslation> SlideTranslations => Set<SlideTranslation>();

    /// <summary>Gets the storefront client testimonials table.</summary>
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();

    /// <summary>Gets the per-locale translations of the storefront client testimonials.</summary>
    public DbSet<TestimonialTranslation> TestimonialTranslations => Set<TestimonialTranslation>();

    /// <summary>Gets the storefront partners table.</summary>
    public DbSet<Partner> Partners => Set<Partner>();

    /// <summary>Gets the per-locale translations of the storefront partners.</summary>
    public DbSet<PartnerTranslation> PartnerTranslations => Set<PartnerTranslation>();

    /// <summary>Gets the storefront server locations table.</summary>
    public DbSet<ServerLocation> ServerLocations => Set<ServerLocation>();

    /// <summary>Gets the per-locale translations of the storefront server locations.</summary>
    public DbSet<ServerLocationTranslation> ServerLocationTranslations => Set<ServerLocationTranslation>();

    /// <summary>Gets the storefront portfolio projects table.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>Gets the per-locale translations of the storefront portfolio projects.</summary>
    public DbSet<ProjectTranslation> ProjectTranslations => Set<ProjectTranslation>();

    /// <summary>Gets the storefront frequently asked questions table.</summary>
    public DbSet<Faq> Faqs => Set<Faq>();

    /// <summary>Gets the per-locale translations of the storefront frequently asked questions.</summary>
    public DbSet<FaqTranslation> FaqTranslations => Set<FaqTranslation>();

    /// <summary>
    /// Gets the Data Protection key ring, shared by every instance so a token protected by one
    /// instance is read by any other and outlives a redeploy. Named by this property like every
    /// other table; the framework entity's own columns (<c>Id</c>, <c>FriendlyName</c>, <c>Xml</c>)
    /// are already PascalCase.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>
    /// Saves every tracked change in one transaction, translating an optimistic-concurrency
    /// conflict to the persistence-agnostic <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Whether the change tracker accepts the changes after a successful save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows written.</returns>
    /// <exception cref="ConcurrencyConflictException">Another writer modified one of the saved aggregates concurrently.</exception>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // The Application layer must not reference EF Core, so the DbContext is the one place that
            // knows the cause was a concurrency conflict. ExceptionHandlingMiddleware answers it 409.
            throw new ConcurrencyConflictException(
                "Another writer modified one of the saved aggregates concurrently.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) => Database.BeginTransactionAsync(ct);

    /// <inheritdoc/>
    public void DetachAll()
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <inheritdoc/>
    public void DiscardPendingChanges()
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
                default:
                    // Unchanged entries are already saved; detached ones are not tracked at all.
                    break;
            }
        }
    }

    /// <inheritdoc/>
    public Task ReloadAsync(object entity, CancellationToken ct) => Entry(entity).ReloadAsync(ct);

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Credentials are encrypted here, from one list, rather than in each configuration: a
        // configuration cannot reach the service, and one that skipped the converter stored the
        // value in plain text without a sign.
        var converter = new EncryptedStringConverter(_encryption);
        foreach (var column in ProtectedColumns.All)
        {
            modelBuilder.Entity(column.Entity).Property(column.Property)
                .HasMaxLength(ProtectedColumnWidth.For(column.MaxPlainLength))
                .HasConversion(converter);
        }

        // The counter behind order numbers; read through INumberSequence so two concurrent orders
        // can never be handed the same number.
        modelBuilder.HasSequence<long>(NumberSequence.OrderSequenceName);

        // Every entity carries DomainEvents (the kernel's BaseEntity); EF must never try to map
        // it. One convention here rather than an Ignore in each configuration; a configuration
        // that also ignores it is harmless.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(BaseEntity.DomainEvents));
            }
        }
    }
}
