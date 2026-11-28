using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="InstalledPlugin"/>.</summary>
public sealed class InstalledPluginConfiguration : IEntityTypeConfiguration<InstalledPlugin>
{
    /// <summary>The longest name an enum member is stored under in a text column.</summary>
    private const int EnumNameLength = 20;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InstalledPlugin> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PluginId).IsRequired().HasMaxLength(PluginIdentifier.MaxLength);
        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.Signer).IsRequired().HasConversion<string>().HasMaxLength(EnumNameLength);
        builder.Property(x => x.SignerKeyId).HasMaxLength(InstalledPlugin.SignerKeyIdLength);
        builder.Property(x => x.SignerPublicKey).HasMaxLength(InstalledPlugin.SignerPublicKeyLength);
        builder.Property(x => x.PackageSizeBytes).IsRequired();
        builder.Property(x => x.PackageSha256).IsRequired().HasMaxLength(InstalledPlugin.Sha256Length);
        builder.Property(x => x.InstalledAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.InstalledByUserId).HasMaxLength(InstalledPlugin.UserLength);
        builder.Property(x => x.InstalledByName).HasMaxLength(InstalledPlugin.UserLength);

        // One record per plugin: the install use cases look the record up by plugin id.
        builder.HasIndex(x => x.PluginId).IsUnique();
    }
}
