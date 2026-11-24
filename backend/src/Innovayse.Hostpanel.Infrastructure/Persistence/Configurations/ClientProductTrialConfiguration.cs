using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ClientProductTrial"/> (table <c>ClientProductTrials</c>).</summary>
public sealed class ClientProductTrialConfiguration : IEntityTypeConfiguration<ClientProductTrial>
{
    /// <summary>
    /// The name of the unique index on the client and the product, the default EF already gave it, named here so
    /// <see cref="AppDbContext"/> can tell its violation from any other.
    /// </summary>
    public const string ClientProductIndex = "IX_ClientProductTrials_ClientId_ProductId";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClientProductTrial> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.StartedAt).IsRequired();

        // No foreign key to the service: the record outlives it, so a client cannot get the trial again by having the
        // service removed.
        // One trial of a product per client, ever; the database refuses a second one.
        builder.HasIndex(x => new { x.ClientId, x.ProductId })
            .IsUnique()
            .HasDatabaseName(ClientProductIndex);
    }
}
