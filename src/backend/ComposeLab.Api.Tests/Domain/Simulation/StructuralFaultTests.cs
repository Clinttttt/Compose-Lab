using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

/// <summary>
/// Faults that stop a run. Every case here must report <c>Completed = false</c>: a learner needs to
/// tell "this architecture has a problem" apart from "this architecture could not be simulated".
/// </summary>
public sealed class StructuralFaultTests
{
    [Fact]
    public void DuplicateServiceName_StopsTheRun()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService { Name = "api", Image = "api:1" },
                new ContainerService { Name = "api", Image = "api:2" }
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Succeeded.ShouldBeFalse();
        result.Completed.ShouldBeFalse();
        result.Only(SimulationIssueCode.DuplicateServiceName).Severity.ShouldBe(SimulationSeverity.Error);
        result.Logged(SimulationEventCode.SimulationAborted).ShouldBeTrue();
        result.Logged(SimulationEventCode.ServiceStarted).ShouldBeFalse();
        result.Logged(SimulationEventCode.NetworkCreated).ShouldBeFalse();
        result.StartOrder.ShouldBeEmpty();
        result.Reachability.ShouldBeEmpty();
    }

    [Fact]
    public void ServiceWithNoImageAndNoBuild_StopsTheRun()
    {
        ApplicationTopology topology = Topologies.With(
            services: [new ContainerService { Name = "api" }]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Completed.ShouldBeFalse();
        result.Only(SimulationIssueCode.ServiceWithoutImageOrBuild).SuggestedFix.ShouldContain("build context");
    }

    [Fact]
    public void BuildContextAlone_IsEnoughToRun()
    {
        ApplicationTopology topology = Topologies.With(
            services: [new ContainerService { Name = "api", BuildContext = "./Api" }]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Has(SimulationIssueCode.ServiceWithoutImageOrBuild).ShouldBeFalse();
        result.Completed.ShouldBeTrue();
    }

    [Fact]
    public void UndeclaredNetwork_StopsTheRun()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService { Name = "api", Image = "api:1", Networks = [Topologies.On("backend")] }
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Completed.ShouldBeFalse();

        SimulationIssue issue = result.Only(SimulationIssueCode.UndeclaredNetwork);

        issue.WhatHappened.ShouldContain("backend");
        issue.Elements.ShouldContain(ElementReference.Network("backend"));
    }

    [Fact]
    public void UndeclaredVolume_StopsTheRun()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Volumes = [new VolumeMount("postgres-data", Topologies.PostgresDataPath)]
                }
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Completed.ShouldBeFalse();
        result.Only(SimulationIssueCode.UndeclaredVolume).WhatHappened.ShouldContain("postgres-data");
    }

    [Fact]
    public void DependencyOnAServiceThatDoesNotExist_StopsTheRun()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Dependencies = [new ServiceDependency("database", DependencyCondition.ServiceStarted)]
                }
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Completed.ShouldBeFalse();
        result.Only(SimulationIssueCode.UnknownDependency).WhatHappened.ShouldContain("database");
    }

    [Fact]
    public void SelfDependency_StopsTheRun()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Dependencies = [new ServiceDependency("api", DependencyCondition.ServiceStarted)]
                }
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Completed.ShouldBeFalse();
        result.Coded(SimulationIssueCode.SelfDependency).ShouldHaveSingleItem();
        result.Has(SimulationIssueCode.DependencyCycle).ShouldBeFalse();
    }

    [Fact]
    public void DependencyCycle_IsReportedOnce()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                Depending("a", "b"),
                Depending("b", "c"),
                Depending("c", "a")
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Completed.ShouldBeFalse();

        SimulationIssue issue = result.Only(SimulationIssueCode.DependencyCycle);

        issue.WhatHappened.ShouldContain("a");
        issue.WhatHappened.ShouldContain("b");
        issue.WhatHappened.ShouldContain("c");
        issue.SuggestedFix.ShouldContain("retry");
    }

    [Fact]
    public void TwoServiceCycle_IsReportedOnce()
    {
        ApplicationTopology topology = Topologies.With(
            services: [Depending("a", "b"), Depending("b", "a")]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Coded(SimulationIssueCode.DependencyCycle).ShouldHaveSingleItem();
    }

    [Fact]
    public void AcyclicDependencies_ProduceNoCycleIssue()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                Depending("web", "api"),
                Depending("api", "db"),
                new ContainerService { Name = "db", Image = "db:1" }
            ]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Has(SimulationIssueCode.DependencyCycle).ShouldBeFalse();
        result.Completed.ShouldBeTrue();
    }

    private static ContainerService Depending(string name, string dependsOn) => new()
    {
        Name = name,
        Image = $"{name}:1",
        Dependencies = [new ServiceDependency(dependsOn, DependencyCondition.ServiceStarted)]
    };
}
