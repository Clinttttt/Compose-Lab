using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Domain.Compose;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.GenerateCompose;

/// <summary>
/// Projects the topology to YAML. Always succeeds: generation is a faithful projection, and reporting
/// what is wrong with an architecture belongs to simulation.
/// </summary>
internal sealed class Handler : IQueryHandler<Query, Response>
{
    public Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ComposeDocument document = ComposeGenerator.Generate(TopologyDocumentMapper.ToTopology(query));

        return Task.FromResult(Result.Success(new Response
        {
            Yaml = document.Yaml,
            Provenance =
            [
                .. document.Provenance.Select(entry => new ProvenanceResponse(
                    ElementResponse.From(entry.Element),
                    entry.Range.StartLine,
                    entry.Range.EndLine))
            ]
        }));
    }
}
