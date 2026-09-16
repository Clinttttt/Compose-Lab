using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

public sealed class PortCollisionTests
{
    [Fact]
    public void TwoServicesClaimingTheSameHostPort_Collide()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                Publishing("api", new PortMapping(8080, 8080)),
                Publishing("web", new PortMapping(8080, 80))
            ]));

        result.Succeeded.ShouldBeFalse();

        SimulationIssue issue = result.Only(SimulationIssueCode.HostPortCollision);

        issue.Severity.ShouldBe(SimulationSeverity.Error);
        issue.WhatHappened.ShouldContain("8080/tcp");
        issue.WhatHappened.ShouldContain("api");
        issue.WhatHappened.ShouldContain("web");
        issue.Elements.Count.ShouldBe(2);
    }

    /// <summary>
    /// A claim is host port plus protocol. TCP 8080 and UDP 8080 are different reservations, so this
    /// must not be reported as a conflict.
    /// </summary>
    [Fact]
    public void SamePortOnDifferentProtocols_DoesNotCollide()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                Publishing("api", new PortMapping(8080, 8080, PortProtocol.Tcp)),
                Publishing("dns", new PortMapping(8080, 53, PortProtocol.Udp))
            ]));

        result.Has(SimulationIssueCode.HostPortCollision).ShouldBeFalse();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void DynamicHostPorts_CannotCollide()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                Publishing("api", new PortMapping(null, 8080)),
                Publishing("web", new PortMapping(null, 8080))
            ]));

        result.Has(SimulationIssueCode.HostPortCollision).ShouldBeFalse();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void OneServiceClaimingTheSameHostPortTwice_Collides()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [Publishing("api", new PortMapping(8080, 8080), new PortMapping(8080, 9090))]));

        result.Only(SimulationIssueCode.HostPortCollision).Severity.ShouldBe(SimulationSeverity.Error);
    }

    [Fact]
    public void DifferentHostPortsOntoTheSameContainerPort_DoNotCollide()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                Publishing("api", new PortMapping(8080, 8080)),
                Publishing("web", new PortMapping(3000, 8080))
            ]));

        result.Has(SimulationIssueCode.HostPortCollision).ShouldBeFalse();
    }

    [Fact]
    public void UnpublishedServices_ReportThatTheyAreReachableByNameInstead()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }]));

        result.Logged(SimulationEventCode.ServiceNotPublished).ShouldBeTrue();
        result.Logged(SimulationEventCode.PortPublished).ShouldBeFalse();
    }

    [Fact]
    public void ADynamicPortIsStillPublished()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [Publishing("api", new PortMapping(null, 8080))]));

        result.Logged(SimulationEventCode.PortPublished).ShouldBeTrue();
        result.Logged(SimulationEventCode.ServiceNotPublished).ShouldBeFalse();

        result.Events
            .Single(item => item.Code == SimulationEventCode.PortPublished)
            .Message.ShouldContain("assigns at run time");
    }

    private static ContainerService Publishing(string name, params PortMapping[] ports) => new()
    {
        Name = name,
        Image = $"{name}:1",
        Ports = ports
    };
}
