using ComposeLab.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComposeLab.Api.DependencyInjection;

public static class PersistenceServices
{
    public const string ConnectionStringName = "ComposeLab";

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"The '{ConnectionStringName}' connection string is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
