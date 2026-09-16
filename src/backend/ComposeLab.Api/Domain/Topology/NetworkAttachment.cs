namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// A service's membership of a network, carrying whether the author wrote the attachment or
/// whether normalization supplied it.
/// </summary>
/// <remarks>
/// Membership is a relationship, never containment. A service may belong to several networks at
/// once — ordinary Compose — so nothing in the model or the renderer may treat a network as owning
/// the services attached to it.
/// </remarks>
public sealed record NetworkAttachment(string NetworkName, DeclarationOrigin Origin);
