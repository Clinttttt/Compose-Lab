namespace ComposeLab.Api.Abstractions.Endpoints;

/// <summary>
/// One HTTP endpoint, discovered by an assembly scan at startup so that adding a slice never
/// requires editing a shared file.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
