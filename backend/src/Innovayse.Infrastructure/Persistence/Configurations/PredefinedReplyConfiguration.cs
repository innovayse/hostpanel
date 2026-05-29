namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="PredefinedReply"/>.</summary>
public sealed class PredefinedReplyConfiguration : IEntityTypeConfiguration<PredefinedReply>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PredefinedReply> builder)
    {
        builder.ToTable("predefined_replies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.CategoryId).IsRequired();

        builder.HasIndex(x => x.CategoryId);
    }
}
