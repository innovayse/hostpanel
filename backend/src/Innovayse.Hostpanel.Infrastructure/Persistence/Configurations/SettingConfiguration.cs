using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Setting"/> aggregate.</summary>
public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    /// <summary>Configures the <c>Settings</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(ProtectedColumnWidth.For(ProtectedValueLimits.LegalTextValue));

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Key).IsUnique();
    }
}
