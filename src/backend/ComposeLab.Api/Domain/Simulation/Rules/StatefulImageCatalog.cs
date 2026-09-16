namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>How much a learner should expect to care about persistence for a given image.</summary>
internal enum PersistenceExpectation
{
    /// <summary>Losing this data on container recreation is almost always a bug.</summary>
    UsuallyDurable,

    /// <summary>Persistence is a legitimate choice either way — a cache may be meant to be disposable.</summary>
    OptionalPersistence
}

internal sealed record StatefulImage(
    string Repository,
    string DisplayName,
    PersistenceExpectation Expectation,
    string DataPath);

/// <summary>
/// Images whose data lives somewhere worth knowing about.
/// </summary>
/// <remarks>
/// This catalog is intentionally incomplete, and that is a known limitation rather than an oversight.
/// Matching an image here produces a persistence lesson; not matching it produces silence, so a
/// learner running <c>timescale/timescaledb</c> or their own image gets no warning. The alternative —
/// warning about every service with no volume — would be noise for APIs, workers, and proxies, which
/// correctly have none.
/// </remarks>
internal static class StatefulImageCatalog
{
    private static readonly StatefulImage[] Entries =
    [
        new("postgres", "PostgreSQL", PersistenceExpectation.UsuallyDurable, "/var/lib/postgresql/data"),
        new("postgresql", "PostgreSQL", PersistenceExpectation.UsuallyDurable, "/var/lib/postgresql/data"),
        new("mysql", "MySQL", PersistenceExpectation.UsuallyDurable, "/var/lib/mysql"),
        new("mariadb", "MariaDB", PersistenceExpectation.UsuallyDurable, "/var/lib/mysql"),
        new("mongo", "MongoDB", PersistenceExpectation.UsuallyDurable, "/data/db"),
        new("mssql/server", "SQL Server", PersistenceExpectation.UsuallyDurable, "/var/opt/mssql"),
        new("redis", "Redis", PersistenceExpectation.OptionalPersistence, "/data"),
        new("rabbitmq", "RabbitMQ", PersistenceExpectation.OptionalPersistence, "/var/lib/rabbitmq")
    ];

    public static StatefulImage? Match(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        string repository = Repository(image);

        return Entries.FirstOrDefault(entry =>
            repository.Equals(entry.Repository, StringComparison.Ordinal)
            || repository.EndsWith('/' + entry.Repository, StringComparison.Ordinal));
    }

    /// <summary>Strips any digest, tag, and casing so that only the repository path remains.</summary>
    private static string Repository(string image)
    {
        string reference = image.Trim();

        int digest = reference.IndexOf('@', StringComparison.Ordinal);

        if (digest >= 0)
        {
            reference = reference[..digest];
        }

        // A colon is only a tag separator when it comes after the last path segment; before that it is
        // a registry port, as in localhost:5000/postgres.
        int lastSegment = reference.LastIndexOf('/');
        int tag = reference.IndexOf(':', lastSegment + 1);

        if (tag >= 0)
        {
            reference = reference[..tag];
        }

        return reference.ToLowerInvariant();
    }
}
