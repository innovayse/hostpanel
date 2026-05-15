namespace Innovayse.Infrastructure.Settings.Configurations;

using Innovayse.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Setting"/> aggregate.</summary>
public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    /// <summary>Configures the <c>settings</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Key).IsUnique();
    }
}
