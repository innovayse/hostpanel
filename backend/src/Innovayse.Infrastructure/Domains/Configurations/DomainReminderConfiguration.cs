namespace Innovayse.Infrastructure.Domains.Configurations;

using Innovayse.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="DomainReminder"/> entity.
/// Maps to the <c>domain_reminders</c> table.
/// </summary>
internal sealed class DomainReminderConfiguration : IEntityTypeConfiguration<DomainReminder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DomainReminder> builder)
    {
        builder.ToTable("domain_reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DomainId).IsRequired();
        builder.Property(r => r.ReminderType).IsRequired().HasMaxLength(100);
        builder.Property(r => r.SentTo).IsRequired().HasMaxLength(320);
        builder.HasIndex(r => r.DomainId);
    }
}
