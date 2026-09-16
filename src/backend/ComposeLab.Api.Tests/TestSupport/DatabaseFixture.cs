using ComposeLab.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace ComposeLab.Api.Tests.TestSupport;

/// <summary>
/// Boots the API against a throwaway PostgreSQL container and applies the migrations.
/// </summary>
/// <remarks>
/// A real Postgres, not SQLite and not the in-memory provider. The behavior worth testing here <em>is</em> the
/// persistence behavior: that a <c>jsonb</c> column round-trips the topology document through Npgsql, and that
/// the migrations apply cleanly to an empty database. A different provider would pass while proving none of it.
/// <para>
/// Separate from <see cref="ApiFixture"/> on purpose, so the engine tests — which touch no database — do not
/// pay for a container.
/// </para>
/// </remarks>
public sealed class DatabaseFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _database.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        });

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        using IServiceScope scope = Services.CreateScope();

        // Migrating here also verifies, on every run, that the migrations apply to an empty database.
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}
