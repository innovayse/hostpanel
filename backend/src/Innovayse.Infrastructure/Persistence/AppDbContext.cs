namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Audit;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Products;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Services;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Slides;
using Innovayse.Domain.Hosting;
using Innovayse.Domain.Ssl;
using Innovayse.Domain.Support;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Root EF Core DbContext for the Innovayse backend.
/// Extends <see cref="IdentityDbContext{TUser}"/> to include ASP.NET Core Identity tables.
/// All entity configurations are discovered automatically via <see cref="OnModelCreating"/>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    /// <summary>Gets the activity log entries table.</summary>
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    /// <summary>Gets the refresh tokens table.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Gets the clients table.</summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>Gets the contacts table.</summary>
    public DbSet<Contact> Contacts => Set<Contact>();

    /// <summary>Gets the product groups table.</summary>
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

    /// <summary>Gets the products table.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Gets the client services table.</summary>
    public DbSet<ClientService> ClientServices => Set<ClientService>();

    /// <summary>Gets the cancellation requests table.</summary>
    public DbSet<CancellationRequest> CancellationRequests => Set<CancellationRequest>();

    /// <summary>Gets the domains table.</summary>
    public DbSet<Domain> Domains => Set<Domain>();

    /// <summary>Gets the billable items table.</summary>
    public DbSet<BillableItem> BillableItems => Set<BillableItem>();

    /// <summary>Gets the invoices table.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Gets the invoice items table.</summary>
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

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

    /// <summary>Gets the slide per-locale translations table.</summary>
    public DbSet<SlideTranslation> SlideTranslations => Set<SlideTranslation>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
