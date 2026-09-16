using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

public sealed class StartOrderTests
{
    [Fact]
    public void ADependencyStartsFirst()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Dependencies = [new ServiceDependency("database", DependencyCondition.ServiceStarted)]
                },
                new ContainerService { Name = "database", Image = "db:1" }
            ]));

        result.StartOrder.ShouldBe(["database", "api"]);
    }

    [Fact]
    public void AChainStartsFromTheDeepestDependency()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                Depending("web", "api"),
                Depending("api", "db"),
                new ContainerService { Name = "db", Image = "db:1" }
            ]));

        result.StartOrder.ShouldBe(["db", "api", "web"]);
    }

    /// <summary>
    /// Alphabetical order among independent services is ComposeLab's presentation choice, not a claim
    /// about Docker, which starts them concurrently. The timeline says so out loud.
    /// </summary>
    [Fact]
    public void IndependentServicesArePresentedAlphabetically()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService { Name = "zebra", Image = "z:1" },
                new ContainerService { Name = "alpha", Image = "a:1" },
                new ContainerService { Name = "mike", Image = "m:1" }
            ]));

        result.StartOrder.ShouldBe(["alpha", "mike", "zebra"]);

        result.Events
            .Single(item => item.Code == SimulationEventCode.StartOrderResolved)
            .Message.ShouldContain("at the same time");
    }

    [Fact]
    public void TheSameArchitectureAlwaysProducesTheSameTimeline()
    {
        ApplicationTopology topology = Topologies.ApiWithPostgres();

        SimulationResult first = TopologySimulator.Simulate(topology);
        SimulationResult second = TopologySimulator.Simulate(topology);

        first.StartOrder.ShouldBe(second.StartOrder);
        first.Events.Select(item => item.Code.Value).ShouldBe(second.Events.Select(item => item.Code.Value));
        first.Events.Select(item => item.Message).ShouldBe(second.Events.Select(item => item.Message));
        first.Issues.Select(issue => issue.Code.Value).ShouldBe(second.Issues.Select(issue => issue.Code.Value));
    }

    [Fact]
    public void StepNumbersAreSequentialAndStartAtOne()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.ApiWithPostgres());

        result.Events.Select(item => item.Step).ShouldBe(Enumerable.Range(1, result.Events.Count));
    }

    [Fact]
    public void TheTimelineFollowsThePipelinePhaseOrder()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.ApiWithPostgres());

        List<int> phases = [.. result.Events.Select(item => (int)item.Phase)];

        phases.ShouldBe([.. phases.Order()]);
        result.Events[0].Phase.ShouldBe(SimulationPhase.Normalization);
        result.Events[^1].Phase.ShouldBe(SimulationPhase.Completion);
    }

    private static ContainerService Depending(string name, string dependsOn) => new()
    {
        Name = name,
        Image = $"{name}:1",
        Dependencies = [new ServiceDependency(dependsOn, DependencyCondition.ServiceStarted)]
    };
}
