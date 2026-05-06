using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotamWatcher.Domain.Entities;

namespace NotamWatcher.Infrastructure.Persistence.Configurations;

internal sealed class WatchedRouteConfiguration : IEntityTypeConfiguration<WatchedRoute>
{
    public void Configure(EntityTypeBuilder<WatchedRoute> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RouteKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.IcaoCodes)
            .IsRequired();

        builder.HasIndex(r => r.RouteKey).IsUnique();
    }
}
