using System.Text.RegularExpressions;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// Infers which services a service intends to talk to, by reading explicit host references out of
/// its environment configuration.
/// </summary>
/// <remarks>
/// Only two forms count as sufficiently explicit: a URL with a scheme and authority, and a
/// key-value connection string whose key is a recognized host key. The key allowlist is what keeps
/// this honest — <c>Username=postgres</c> must not be read as an intent to reach a service named
/// <c>postgres</c>, and <c>Database=app</c> must not be read as an intent to reach <c>app</c>.
/// <para>
/// This is a heuristic and it is labeled as one wherever it surfaces. Environment variable names are
/// deliberately not inspected, so a bare <c>DB_HOST=database</c> yields no intent edge. Widening
/// that is a later decision, not an accident.
/// </para>
/// </remarks>
internal static partial class ConnectionIntentInference
{
    private static readonly string[] HostKeys =
    [
        "host",
        "hostname",
        "server",
        "data source",
        "datasource",
        "address",
        "addr"
    ];

    public static IReadOnlyList<InferredConnection> Infer(NormalizedTopology topology)
    {
        HashSet<string> serviceNames = [.. topology.Services.Select(service => service.Name)];
        List<InferredConnection> connections = [];

        foreach (ContainerService service in topology.Services)
        {
            foreach (KeyValuePair<string, string> variable in service.Environment
                .OrderBy(variable => variable.Key, StringComparer.Ordinal))
            {
                foreach (string host in ExtractHosts(variable.Value))
                {
                    if (host == service.Name || !serviceNames.Contains(host))
                    {
                        continue;
                    }

                    bool alreadyRecorded = connections.Any(connection =>
                        connection.FromService == service.Name
                        && connection.ToService == host
                        && connection.EnvironmentKey == variable.Key);

                    if (!alreadyRecorded)
                    {
                        connections.Add(new InferredConnection(service.Name, host, variable.Key, host));
                    }
                }
            }
        }

        return connections;
    }

    private static IEnumerable<string> ExtractHosts(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (Match match in UriPattern().Matches(value))
        {
            string host = HostFromAuthority(match.Groups["authority"].Value);

            if (host.Length > 0)
            {
                yield return host;
            }
        }

        foreach (string segment in value.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = segment.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            string key = segment[..separator].Trim().ToLowerInvariant();

            if (!HostKeys.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            string host = HostFromConnectionStringValue(segment[(separator + 1)..].Trim());

            if (host.Length > 0)
            {
                yield return host;
            }
        }
    }

    private static string HostFromAuthority(string authority)
    {
        int credentials = authority.LastIndexOf('@');

        if (credentials >= 0)
        {
            authority = authority[(credentials + 1)..];
        }

        if (authority.StartsWith('['))
        {
            int close = authority.IndexOf(']', StringComparison.Ordinal);

            return close > 1 ? authority[1..close] : string.Empty;
        }

        int port = authority.IndexOf(':', StringComparison.Ordinal);

        return port >= 0 ? authority[..port] : authority;
    }

    private static string HostFromConnectionStringValue(string value)
    {
        // SQL Server writes the port after a comma; most others use a colon.
        int comma = value.IndexOf(',', StringComparison.Ordinal);

        if (comma >= 0)
        {
            value = value[..comma];
        }

        int colon = value.IndexOf(':', StringComparison.Ordinal);

        if (colon >= 0)
        {
            value = value[..colon];
        }

        return value.Trim();
    }

    [GeneratedRegex(
        "[a-zA-Z][a-zA-Z0-9+.\\-]*://(?<authority>[^/\\s;,\"']+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex UriPattern();
}
