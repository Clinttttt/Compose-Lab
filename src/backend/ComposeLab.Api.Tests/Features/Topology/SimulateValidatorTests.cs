using ComposeLab.Api.Features.Topology.Shared;
using ComposeLab.Api.Features.Topology.Simulate;
using FluentValidation.TestHelper;

namespace ComposeLab.Api.Tests.Features.Topology;

/// <summary>
/// The validator guards the request envelope. The tests at the bottom of this file are the important
/// ones: they pin down that a wrong architecture is still a valid request, because rejecting those
/// would leave ComposeLab unable to explain the mistakes it exists to teach.
/// </summary>
public sealed class SimulateValidatorTests
{
    private readonly Validator _validator = new();

    [Fact]
    public void AMinimalTopologyIsValid() =>
        _validator.TestValidate(Query([Service("api", image: "api:1")])).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AnEmptyTopologyIsValid() =>
        _validator.TestValidate(new Query()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ServiceNameIsRequired() =>
        _validator.TestValidate(Query([Service(string.Empty, image: "api:1")]))
            .ShouldHaveValidationErrorFor("Services[0].Name");

    [Theory]
    [InlineData("-api")]
    [InlineData("_api")]
    [InlineData("my api")]
    [InlineData("api/two")]
    public void ServiceNameMustBeAValidComposeName(string name) =>
        _validator.TestValidate(Query([Service(name, image: "api:1")]))
            .ShouldHaveValidationErrorFor("Services[0].Name");

    [Theory]
    [InlineData("api")]
    [InlineData("api-gateway")]
    [InlineData("api_gateway")]
    [InlineData("api.gateway")]
    [InlineData("db2")]
    public void ReasonableServiceNamesAreAccepted(string name) =>
        _validator.TestValidate(Query([Service(name, image: "api:1")])).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ContainerPortMustBeInRange(int containerPort)
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            Ports = [new PortRequest { ContainerPort = containerPort }]
        };

        _validator.TestValidate(Query([service]))
            .ShouldHaveValidationErrorFor("Services[0].Ports[0].ContainerPort");
    }

    [Fact]
    public void HostPortMustBeInRangeWhenSupplied()
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            Ports = [new PortRequest { HostPort = 0, ContainerPort = 8080 }]
        };

        _validator.TestValidate(Query([service]))
            .ShouldHaveValidationErrorFor("Services[0].Ports[0].HostPort");
    }

    [Fact]
    public void AnOmittedHostPortIsValid()
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            Ports = [new PortRequest { ContainerPort = 8080 }]
        };

        _validator.TestValidate(Query([service])).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProtocolMustBeTcpOrUdp()
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            Ports = [new PortRequest { ContainerPort = 8080, Protocol = "sctp" }]
        };

        _validator.TestValidate(Query([service]))
            .ShouldHaveValidationErrorFor("Services[0].Ports[0].Protocol");
    }

    [Theory]
    [InlineData("tcp")]
    [InlineData("udp")]
    [InlineData("TCP")]
    public void RecognizedProtocolsAreAccepted(string protocol)
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            Ports = [new PortRequest { ContainerPort = 8080, Protocol = protocol }]
        };

        _validator.TestValidate(Query([service])).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AMountPathMustBeAbsolute()
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            Volumes = [new VolumeMountRequest { Volume = "data", Path = "var/lib/data" }]
        };

        _validator.TestValidate(Query([service]))
            .ShouldHaveValidationErrorFor("Services[0].Volumes[0].Path");
    }

    [Fact]
    public void ADependencyConditionMustBeRecognized()
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            DependsOn = [new DependencyRequest { Service = "db", Condition = "service_completed" }]
        };

        _validator.TestValidate(Query([service]))
            .ShouldHaveValidationErrorFor("Services[0].DependsOn[0].Condition");
    }

    [Fact]
    public void AnOmittedConditionIsValid()
    {
        ServiceRequest service = Service("api", image: "api:1") with
        {
            DependsOn = [new DependencyRequest { Service = "db" }]
        };

        _validator.TestValidate(Query([service])).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TooManyServicesIsRejected()
    {
        ServiceRequest[] services = [.. Enumerable.Range(0, 51).Select(index => Service($"s{index}", "api:1"))];

        _validator.TestValidate(new Query { Services = services })
            .ShouldHaveValidationErrorFor(query => query.Services);
    }

    // --- The architecture may be wrong. That is not the validator's business. ---

    [Fact]
    public void DuplicateServiceNamesAreAValidRequest() =>
        _validator.TestValidate(Query([Service("api", "api:1"), Service("api", "api:2")]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AReferenceToAnUndeclaredNetworkIsAValidRequest() =>
        _validator.TestValidate(Query([Service("api", "api:1") with { Networks = ["backend"] }]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AServiceWithNoImageAndNoBuildIsAValidRequest() =>
        _validator.TestValidate(Query([Service("api")])).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CollidingHostPortsAreAValidRequest()
    {
        ServiceRequest first = Service("api", "api:1") with
        {
            Ports = [new PortRequest { HostPort = 8080, ContainerPort = 8080 }]
        };

        ServiceRequest second = Service("web", "web:1") with
        {
            Ports = [new PortRequest { HostPort = 8080, ContainerPort = 80 }]
        };

        _validator.TestValidate(Query([first, second])).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ADependencyOnAMissingServiceIsAValidRequest() =>
        _validator.TestValidate(Query(
            [Service("api", "api:1") with { DependsOn = [new DependencyRequest { Service = "nope" }] }]))
            .ShouldNotHaveAnyValidationErrors();

    private static Query Query(IReadOnlyList<ServiceRequest> services) => new() { Services = services };

    private static ServiceRequest Service(string name, string? image = null) => new()
    {
        Name = name,
        Image = image
    };
}
