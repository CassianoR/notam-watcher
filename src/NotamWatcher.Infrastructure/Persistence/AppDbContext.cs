using Microsoft.EntityFrameworkCore;
using NotamWatcher.Domain.Entities;

namespace NotamWatcher.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Notam> Notams => Set<Notam>();
    public DbSet<WatchedRoute> WatchedRoutes => Set<WatchedRoute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
