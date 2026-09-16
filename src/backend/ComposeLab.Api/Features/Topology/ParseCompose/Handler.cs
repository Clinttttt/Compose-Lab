using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Domain.Compose;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.ParseCompose;

/// <summary>
/// Reads the file and hands back either a topology or the findings that stopped it.
/// </summary>
/// <remarks>
/// Always a 200. A file ComposeLab cannot apply is a successful analysis with an explanation, the same
/// way a broken architecture is — returning 4xx would say the request was malformed when the request was
/// perfectly well formed and the file was not.
/// </remarks>
internal sealed class Handler : IQueryHandler<Query, Response>
{
    public Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ComposeParseResult result = ComposeParser.Parse(query.Yaml);

        return Task.FromResult(Result.Success(new Response
        {
            CanApply = result.CanApply,
            Topology = result.Topology is null ? null : TopologyMapper.ToRequest(result.Topology),
            Findings =
            [
                .. result.Findings.Select(finding => new ComposeFindingResponse(
                    finding.Code.Value,
                    finding.Path,
                    finding.Line,
                    finding.Column,
                    finding.Message,
                    finding.Suggestion))
            ]
        }));
    }
}
