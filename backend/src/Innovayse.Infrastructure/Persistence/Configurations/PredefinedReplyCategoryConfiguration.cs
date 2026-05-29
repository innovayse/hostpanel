namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="PredefinedReplyCategory"/>.</summary>
public sealed class PredefinedReplyCategoryConfiguration : IEntityTypeConfiguration<PredefinedReplyCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PredefinedReplyCategory> builder)
    {
        builder.ToTable("predefined_reply_categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.ParentCategoryId);
    }
}
