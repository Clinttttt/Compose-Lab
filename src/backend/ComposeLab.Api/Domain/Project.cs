using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Domain;

/// <summary>
/// A saved architecture: a name, the authored topology, and when it changed.
/// </summary>
/// <remarks>
/// Saving is deliberately not a quality gate. A learner may save an architecture with duplicate service
/// names, colliding ports, or a connection that cannot work, because building and inspecting those mistakes
/// is the product. The engine explains them; storage just keeps them.
/// <para>
/// The topology is stored as one document and replaced wholesale. There is no history, no ownership, and no
/// autosave — those are separate decisions, not yet taken.
/// </para>
/// </remarks>
public sealed class Project
{
    private Project(
        Guid id,
        string name,
        TopologyDocument topology,
        int topologySchemaVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Topology = topology;
        TopologySchemaVersion = topologySchemaVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>The authored topology, never a normalized one.</summary>
    public TopologyDocument Topology { get; private set; }

    /// <summary>
    /// The contract version the stored document was written against. Read paths must check this rather than
    /// assuming the current shape.
    /// </summary>
    public int TopologySchemaVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasReadableTopology => TopologySchemaVersion == TopologySchema.CurrentVersion;

    public static Project Create(string name, TopologyDocument topology, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(topology);

        DateTimeOffset timestamp = now.ToUniversalTime();

        return new Project(Guid.CreateVersion7(), name.Trim(), topology, TopologySchema.CurrentVersion,
            timestamp, timestamp);
    }

    /// <summary>
    /// Replaces the saved name and topology in one step, and stamps the current schema version — saving is
    /// how a project written against an older contract gets brought forward.
    /// </summary>
    public void Replace(string name, TopologyDocument topology, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(topology);

        Name = name.Trim();
        Topology = topology;
        TopologySchemaVersion = TopologySchema.CurrentVersion;
        UpdatedAt = now.ToUniversalTime();
    }
}
