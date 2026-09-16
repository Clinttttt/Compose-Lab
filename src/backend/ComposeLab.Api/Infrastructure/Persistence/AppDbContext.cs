using ComposeLab.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ComposeLab.Api.Infrastructure.Persistence;

/// <summary>
/// The composition root for the data model, the way <c>Program.cs</c> is for endpoints. It is the one place
/// that legitimately names every entity.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
