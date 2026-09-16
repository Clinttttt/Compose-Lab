using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// Resolves the order in which services are presented as starting.
/// </summary>
/// <remarks>
/// A dependency-respecting topological order, with ties broken alphabetically so that the same
/// architecture always produces the same timeline. The alphabetical part is presentation only: real
/// Compose starts services with no dependency between them concurrently, and no message may imply
/// otherwise.
/// </remarks>
internal static class ServiceStartOrder
{
    public static IReadOnlyList<string> Resolve(NormalizedTopology topology)
    {
        HashSet<string> names = [.. topology.Services.Select(service => service.Name)];

        Dictionary<string, HashSet<string>> dependencies = topology.Services
            .GroupBy(service => service.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(service => service.Dependencies)
                    .Select(dependency => dependency.ServiceName)
                    .Where(name => names.Contains(name) && name != group.Key)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        List<string> order = [];
        HashSet<string> started = new(StringComparer.Ordinal);
        SortedSet<string> remaining = new(names, StringComparer.Ordinal);

        while (remaining.Count > 0)
        {
            string? next = remaining.FirstOrDefault(name => dependencies[name].IsSubsetOf(started));

            if (next is null)
            {
                // Unreachable in practice: cycles are reported as structural errors and stop the run
                // before ordering. Falling back keeps the method total rather than throwing.
                order.AddRange(remaining);
                break;
            }

            order.Add(next);
            started.Add(next);
            remaining.Remove(next);
        }

        return order;
    }
}
