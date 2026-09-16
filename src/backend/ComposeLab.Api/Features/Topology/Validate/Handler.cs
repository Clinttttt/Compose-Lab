using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.Validate;

/// <summary>
/// Checks the architecture and returns the findings.
/// </summary>
/// <remarks>
/// Always succeeds, for the same reason simulate does: an invalid architecture is a successful analysis
/// with an explanation, not a failed request.
/// </remarks>
internal sealed class Handler : IQueryHandler<Query, Response>
{
    public Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidationReport report = TopologyValidator.Validate(TopologyDocumentMapper.ToTopology(query));

        return Task.FromResult(Result.Success(new Response
        {
            IsValid = report.IsValid,
            IsComplete = report.IsComplete,
            Issues = [.. report.Issues.Select(ArchitectureIssueResponse.From)]
        }));
    }
}
