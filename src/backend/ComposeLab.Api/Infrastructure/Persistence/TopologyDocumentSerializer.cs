using System.Text.Json;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Infrastructure.Persistence;

/// <summary>
/// Turns the authored topology into the JSON stored in the <c>jsonb</c> column, and back.
/// </summary>
/// <remarks>
/// The document is kept opaque to EF on purpose. Mapping it with <c>ToJson()</c> would require EF to model
/// every nested type, which in turn would mean replacing the document's read-only collections and its
/// dictionary with EF-friendly shapes — distorting a domain contract to suit the mapper. ComposeLab replaces
/// and reads the topology wholesale and has no need for JSON-path queries, so there is nothing to buy with
/// that trade.
/// <para>
/// Web defaults are used so the stored JSON matches the wire shape exactly, which makes a row readable in
/// <c>psql</c> without translation.
/// </para>
/// </remarks>
internal static class TopologyDocumentSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(TopologyDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static TopologyDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<TopologyDocument>(json, Options)
        ?? throw new InvalidOperationException("A stored topology document could not be read.");
}
