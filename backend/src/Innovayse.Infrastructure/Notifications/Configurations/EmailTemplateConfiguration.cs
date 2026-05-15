namespace Innovayse.Infrastructure.Notifications.Configurations;

using Innovayse.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="EmailTemplate"/> aggregate.</summary>
public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    /// <summary>Configures the <c>email_templates</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique();

        builder.HasData(
            new
            {
                Id = 1,
                Slug = "welcome",
                Subject = "Welcome to Innovayse!",
                Body = "<h1>Welcome!</h1><p>Hello, your account has been created successfully.</p>",
                IsActive = true,
                Description = "Sent on client registration",
            },
            new
            {
                Id = 2,
                Slug = "invoice-created",
                Subject = "Invoice #{{invoice.id}} Created",
                Body = "<p>Your invoice for {{invoice.total}} is ready.</p>",
                IsActive = true,
                Description = "Sent when invoice created",
            },
            new
            {
                Id = 3,
                Slug = "payment-received",
                Subject = "Payment Received",
                Body = "<p>Thank you for your payment of {{invoice.total}}.</p>",
                IsActive = true,
                Description = "Sent on payment",
            },
            new
            {
                Id = 4,
                Slug = "service-provisioned",
                Subject = "Your Service is Ready",
                Body = "<p>Your hosting service has been provisioned.</p>",
                IsActive = true,
                Description = "Sent when service provisioned",
            },
            new
            {
                Id = 5,
                Slug = "ticket-created",
                Subject = "Support Ticket #{{ticket.id}} Created",
                Body = "<p>We received your support request.</p>",
                IsActive = true,
                Description = "Sent when ticket created",
            });
    }
}
