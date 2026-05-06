using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotamWatcher.Domain.Entities;

namespace NotamWatcher.Infrastructure.Persistence.Configurations;

internal sealed class NotamConfiguration : IEntityTypeConfiguration<Notam>
{
    public void Configure(EntityTypeBuilder<Notam> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.NotamNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.IcaoLocation)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(n => n.QCode)
            .HasMaxLength(10);

        builder.Property(n => n.Subject)
            .HasMaxLength(100);

        builder.Property(n => n.Condition)
            .HasMaxLength(100);

        builder.Property(n => n.FreeText)
            .IsRequired();

        builder.Property(n => n.RawText)
            .IsRequired();

        // Unique constraint prevents duplicate imports on repeated fetches.
        builder.HasIndex(n => n.NotamNumber).IsUnique();

        builder.HasIndex(n => n.IcaoLocation);
        builder.HasIndex(n => n.EndValidity);
    }
}
