using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Infrastructure.Validation;

/// <summary>
/// The envelope validator for a bare topology document, for slices that carry one as a property rather than
/// as the request body itself.
/// </summary>
internal sealed class TopologyDocumentEnvelopeValidator : TopologyDocumentValidator<TopologyDocument>;
