namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Products;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Services;
using Innovayse.Domain.Settings;
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

    /// <summary>Gets the invoices table.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Gets the invoice items table.</summary>
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    /// <summary>Gets the support tickets table.</summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>Gets the support departments table.</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>Gets the knowledge base articles table.</summary>
    public DbSet<KbArticle> KbArticles => Set<KbArticle>();

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

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
