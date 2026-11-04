using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SshKey"/> (table <c>SshKeys</c>).</summary>
public sealed class SshKeyConfiguration : IEntityTypeConfiguration<SshKey>
{
    /// <summary>
    /// The name of the unique index on a service's live key fingerprints, the default EF already gave it, named here so
    /// <see cref="AppDbContext"/> can tell its violation from any other.
    /// </summary>
    public const string ServiceFingerprintIndex = "IX_SshKeys_ClientServiceId_Fingerprint";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SshKey> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.KeyId).IsRequired();
        builder.HasIndex(x => x.KeyId).IsUnique();
        builder.Property(x => x.AccountUsername).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PublicKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Fingerprint).IsRequired().HasMaxLength(60);
        builder.Property(x => x.SyncState).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LastSyncError).HasConversion(new SshProviderFailureConverter()).HasMaxLength(64);

        // The plugin id rule (PluginIdentifier) caps an id at 64 characters; null for a key the client added.
        builder.Property(x => x.IssuedToPluginId).HasMaxLength(64);

        builder.Ignore(x => x.IsActive);

        builder.HasOne<ClientService>()
            .WithMany()
            .HasForeignKey(x => x.ClientServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // No foreign key to Servers on purpose: the pinned server id must survive the server row
        // being deleted, so the record still says where the key was installed.
        builder.HasIndex(x => x.ServerId);

        // The database, not only the handler, refuses the same key twice on one service, so two
        // concurrent adds cannot both be recorded. Revoked rows are history and do not count.
        builder.HasIndex(x => new { x.ClientServiceId, x.Fingerprint })
            .IsUnique()
            .HasFilter("\"SyncState\" <> 'Revoked'")
            .HasDatabaseName(ServiceFingerprintIndex);
    }
}
