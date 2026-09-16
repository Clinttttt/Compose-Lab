using System.Text.RegularExpressions;
using FluentValidation;

namespace ComposeLab.Api.Features.Topology.Shared;

/// <summary>
/// Guards the request envelope for any slice that accepts a topology.
/// </summary>
/// <remarks>
/// This rejects input that is malformed — a port outside the valid range, a relative container path, an
/// unrecognized protocol. It must never reject an architecture that is merely wrong: duplicate service
/// names, references to undeclared networks, colliding host ports, and services with no image are all
/// valid input, because explaining them is the product. A validator that rejected them would make
/// ComposeLab unable to teach the mistakes it exists for.
/// <para>
/// The collection limits are a deliberate bound on work per request. These endpoints are
/// unauthenticated in this phase, so an unbounded topology would be an easy way to burn CPU.
/// </para>
/// </remarks>
internal abstract class TopologyRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : TopologyRequest
{
    private const int MaxServices = 50;
    private const int MaxDeclarations = 50;

    protected TopologyRequestValidator()
    {
        RuleFor(request => request.Services)
            .Must(services => services.Count <= MaxServices)
            .WithMessage($"A topology may contain at most {MaxServices} services.");

        RuleFor(request => request.Networks)
            .Must(networks => networks.Count <= MaxDeclarations)
            .WithMessage($"A topology may declare at most {MaxDeclarations} networks.");

        RuleFor(request => request.Volumes)
            .Must(volumes => volumes.Count <= MaxDeclarations)
            .WithMessage($"A topology may declare at most {MaxDeclarations} volumes.");

        RuleForEach(request => request.Services).SetValidator(new ServiceRequestValidator());
        RuleForEach(request => request.Networks).SetValidator(new NetworkRequestValidator());
        RuleForEach(request => request.Volumes).SetValidator(new VolumeRequestValidator());
    }
}

internal sealed class ServiceRequestValidator : AbstractValidator<ServiceRequest>
{
    private const int MaxPorts = 20;
    private const int MaxNetworks = 20;
    private const int MaxVolumes = 20;
    private const int MaxDependencies = 50;
    private const int MaxEnvironmentVariables = 100;

    public ServiceRequestValidator()
    {
        RuleFor(service => service.Name)
            .NotEmpty()
            .MaximumLength(63)
            .Must(name => ComposeNames.IsValid(name))
            .When(service => !string.IsNullOrEmpty(service.Name), ApplyConditionTo.CurrentValidator)
            .WithMessage("A service name may contain letters, digits, dots, underscores, and hyphens, and must "
                + "start with a letter or digit.");

        RuleFor(service => service.Image).MaximumLength(255);
        RuleFor(service => service.Build).MaximumLength(1024);

        RuleFor(service => service.Ports)
            .Must(ports => ports.Count <= MaxPorts)
            .WithMessage($"A service may publish at most {MaxPorts} ports.");

        RuleFor(service => service.Networks)
            .Must(networks => networks.Count <= MaxNetworks)
            .WithMessage($"A service may join at most {MaxNetworks} networks.");

        RuleFor(service => service.Volumes)
            .Must(volumes => volumes.Count <= MaxVolumes)
            .WithMessage($"A service may mount at most {MaxVolumes} volumes.");

        RuleFor(service => service.DependsOn)
            .Must(dependencies => dependencies.Count <= MaxDependencies)
            .WithMessage($"A service may declare at most {MaxDependencies} dependencies.");

        RuleFor(service => service.Environment)
            .Must(environment => environment.Count <= MaxEnvironmentVariables)
            .WithMessage($"A service may declare at most {MaxEnvironmentVariables} environment variables.")
            .Must(environment => environment.Keys.All(key => !string.IsNullOrWhiteSpace(key)))
            .WithMessage("An environment variable name cannot be empty.");

        RuleForEach(service => service.Ports).SetValidator(new PortRequestValidator());
        RuleForEach(service => service.Volumes).SetValidator(new VolumeMountRequestValidator());
        RuleForEach(service => service.DependsOn).SetValidator(new DependencyRequestValidator());

        RuleFor(service => service.HealthCheck!)
            .SetValidator(new HealthCheckRequestValidator())
            .When(service => service.HealthCheck is not null);

        RuleForEach(service => service.Networks)
            .NotEmpty()
            .WithMessage("A network name cannot be empty.");
    }
}

internal sealed class PortRequestValidator : AbstractValidator<PortRequest>
{
    private static readonly string[] Protocols = ["tcp", "udp"];

    public PortRequestValidator()
    {
        RuleFor(port => port.ContainerPort).InclusiveBetween(1, 65535);

        RuleFor(port => port.HostPort)
            .InclusiveBetween(1, 65535)
            .When(port => port.HostPort is not null);

        RuleFor(port => port.Protocol)
            .Must(protocol => Protocols.Contains(protocol!.ToLowerInvariant(), StringComparer.Ordinal))
            .When(port => port.Protocol is not null)
            .WithMessage("A port protocol must be 'tcp' or 'udp'.");
    }
}

internal sealed class VolumeMountRequestValidator : AbstractValidator<VolumeMountRequest>
{
    public VolumeMountRequestValidator()
    {
        RuleFor(mount => mount.Volume).NotEmpty().MaximumLength(255);

        RuleFor(mount => mount.Path)
            .NotEmpty()
            .MaximumLength(1024)
            .Must(path => path.StartsWith('/'))
            .When(mount => !string.IsNullOrEmpty(mount.Path), ApplyConditionTo.CurrentValidator)
            .WithMessage("A mount path must be an absolute path inside the container, starting with '/'.");
    }
}

internal sealed class DependencyRequestValidator : AbstractValidator<DependencyRequest>
{
    private static readonly string[] Conditions = ["service_started", "service_healthy"];

    public DependencyRequestValidator()
    {
        RuleFor(dependency => dependency.Service).NotEmpty().MaximumLength(63);

        RuleFor(dependency => dependency.Condition)
            .Must(condition => Conditions.Contains(condition!.ToLowerInvariant(), StringComparer.Ordinal))
            .When(dependency => dependency.Condition is not null)
            .WithMessage("A dependency condition must be 'service_started' or 'service_healthy'.");
    }
}

/// <summary>
/// A healthcheck must name a form ComposeLab understands, and carry the arguments that form requires.
/// Rejecting a mismatch here is what guarantees every accepted healthcheck has a valid Compose form to
/// generate.
/// </summary>
internal sealed class HealthCheckRequestValidator : AbstractValidator<HealthCheckRequest>
{
    public HealthCheckRequestValidator()
    {
        RuleFor(healthCheck => healthCheck.Form)
            .Must(TopologyMapper.HealthCheckForms.IsRecognized)
            .WithMessage("A healthcheck form must be 'disabled', 'shell', 'command', or 'command_shell'.");

        RuleFor(healthCheck => healthCheck.Test)
            .Must(test => test.Count == 1)
            .When(healthCheck => TopologyMapper.HealthCheckForms.IsSingleCommand(healthCheck.Form))
            .WithMessage("The 'shell' and 'command_shell' forms take exactly one command string.");

        RuleFor(healthCheck => healthCheck.Test)
            .Must(test => test.Count > 0)
            .When(healthCheck => string.Equals(
                healthCheck.Form,
                TopologyMapper.HealthCheckForms.Command,
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("The 'command' form needs at least one argument, and must not include the leading "
                + "'CMD' token.");

        RuleFor(healthCheck => healthCheck.Test)
            .Must(test => test.Count == 0)
            .When(healthCheck => TopologyMapper.HealthCheckForms.IsDisabled(healthCheck.Form))
            .WithMessage("A disabled healthcheck has no test command.");

        RuleForEach(healthCheck => healthCheck.Test)
            .NotEmpty()
            .WithMessage("A healthcheck test cannot contain empty entries.");
    }
}

internal sealed class NetworkRequestValidator : AbstractValidator<NetworkRequest>
{
    public NetworkRequestValidator() =>
        RuleFor(network => network.Name)
            .NotEmpty()
            .MaximumLength(63)
            .Must(name => ComposeNames.IsValid(name))
            .When(network => !string.IsNullOrEmpty(network.Name), ApplyConditionTo.CurrentValidator)
            .WithMessage("A network name may contain letters, digits, dots, underscores, and hyphens, and must "
                + "start with a letter or digit.");
}

internal sealed class VolumeRequestValidator : AbstractValidator<VolumeRequest>
{
    public VolumeRequestValidator() =>
        RuleFor(volume => volume.Name)
            .NotEmpty()
            .MaximumLength(63)
            .Must(name => ComposeNames.IsValid(name))
            .When(volume => !string.IsNullOrEmpty(volume.Name), ApplyConditionTo.CurrentValidator)
            .WithMessage("A volume name may contain letters, digits, dots, underscores, and hyphens, and must "
                + "start with a letter or digit.");
}

internal static partial class ComposeNames
{
    public static bool IsValid(string name) => Pattern().IsMatch(name);

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
