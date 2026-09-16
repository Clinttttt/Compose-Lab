using ComposeLab.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComposeLab.Api.Tests.TestSupport;

/// <summary>
/// A test against the real database. Truncates between tests rather than recreating the container, which is
/// milliseconds instead of seconds.
/// </summary>
public abstract class DatabaseTestBase : IAsyncLifetime
{
    private IServiceScope _scope = null!;

    protected DatabaseTestBase(DatabaseFixture fixture) => Fixture = fixture;

    protected DatabaseFixture Fixture { get; }

    protected HttpClient Client { get; private set; } = null!;

    protected AppDbContext Context { get; private set; } = null!;

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Client = Fixture.CreateClient();
        _scope = Fixture.Services.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE projects", Token);
    }

    public ValueTask DisposeAsync()
    {
        _scope.Dispose();
        Client.Dispose();

        return ValueTask.CompletedTask;
    }
}
