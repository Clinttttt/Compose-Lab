using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

public sealed class ReachabilityTests
{
    [Fact]
    public void SharedNetwork_AllowsTheIntendedConnection()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.ApiWithPostgres());

        result.Has(SimulationIssueCode.NetworkUnreachable).ShouldBeFalse();
        result.Succeeded.ShouldBeTrue();
        result.Completed.ShouldBeTrue();
        result.Reachability.ShouldHaveSingleItem().CanCommunicate.ShouldBeTrue();
        result.Reachability[0].SharedNetworks.ShouldBe(["backend"]);
    }

    /// <summary>
    /// The single most important test in the suite. The most common beginner Compose file declares no
    /// networks at all and works perfectly in Docker, because Compose supplies one. Reporting it as
    /// broken would make ComposeLab teach something false.
    /// </summary>
    [Fact]
    public void ServicesThatDeclareNoNetworks_CanStillReachEachOther()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Environment = Topologies.Env(("ConnectionStrings__Database", "Host=database;Database=app"))
                },
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Volumes = [new VolumeMount("data", Topologies.PostgresDataPath)]
                }
            ],
            volumes: ["data"]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Has(SimulationIssueCode.NetworkUnreachable).ShouldBeFalse();
        result.Errors().ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();
        result.Reachability.ShouldHaveSingleItem().CanCommunicate.ShouldBeTrue();
    }

    [Fact]
    public void DifferentNetworks_BreakAnIntendedConnection_WithAnExplanation()
    {
        SimulationResult result = TopologySimulator.Simulate(
            Topologies.ApiWithPostgres(apiNetwork: "frontend", databaseNetwork: "backend"));

        result.Succeeded.ShouldBeFalse();
        result.Completed.ShouldBeTrue();

        SimulationIssue issue = result.Only(SimulationIssueCode.NetworkUnreachable);

        issue.Severity.ShouldBe(SimulationSeverity.Error);
        issue.WhatHappened.ShouldBe("'api' cannot reach 'database'.");
        issue.Why.ShouldContain("frontend");
        issue.Why.ShouldContain("backend");
        issue.ArchitectureBehavior.ShouldContain("ConnectionStrings__Database");
        issue.SuggestedFix.ShouldContain("shared network");

        issue.Elements.ShouldContain(ElementReference.Service("api"));
        issue.Elements.ShouldContain(ElementReference.Service("database"));
        issue.Elements.ShouldContain(
            ElementReference.EnvironmentVariable("api", "ConnectionStrings__Database"));
    }

    /// <summary>
    /// A dependency expresses lifecycle ordering, not network traffic. Two services on separate
    /// networks with a dependency between them and no host reference is a reportable fact, not a fault.
    /// </summary>
    [Fact]
    public void DependsOnAlone_IsNotEvidenceOfCommunication()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Networks = [Topologies.On("frontend")],
                    Dependencies = [new ServiceDependency("database", DependencyCondition.ServiceStarted)]
                },
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Networks = [Topologies.On("backend")],
                    Volumes = [new VolumeMount("data", Topologies.PostgresDataPath)]
                }
            ],
            networks: ["frontend", "backend"],
            volumes: ["data"]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Has(SimulationIssueCode.NetworkUnreachable).ShouldBeFalse();
        result.InferredConnections.ShouldBeEmpty();
        result.Errors().ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();

        // The fact is still reported, as information.
        result.Reachability.ShouldHaveSingleItem().CanCommunicate.ShouldBeFalse();
    }

    [Fact]
    public void UnreachablePairsWithNoIntent_AreInformationOnly()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService { Name = "worker", Image = "worker:1", Networks = [Topologies.On("jobs")] },
                new ContainerService { Name = "web", Image = "web:1", Networks = [Topologies.On("public")] }
            ],
            networks: ["jobs", "public"]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Succeeded.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        result.Reachability.ShouldHaveSingleItem().CanCommunicate.ShouldBeFalse();
    }

    [Fact]
    public void AServiceOnTwoNetworks_BridgesTheTiersItBelongsTo()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService { Name = "proxy", Image = "nginx:1", Networks = [Topologies.On("frontend")] },
                new ContainerService
                {
                    Name = "app",
                    Image = "app:1",
                    Networks = [Topologies.On("frontend"), Topologies.On("backend")],
                    Environment = Topologies.Env(("DATABASE_URL", "postgres://user:pw@db:5432/app"))
                },
                new ContainerService
                {
                    Name = "db",
                    Image = Topologies.PostgresImage,
                    Networks = [Topologies.On("backend")],
                    Volumes = [new VolumeMount("data", Topologies.PostgresDataPath)]
                }
            ],
            networks: ["frontend", "backend"],
            volumes: ["data"]);

        SimulationResult result = TopologySimulator.Simulate(topology);

        result.Succeeded.ShouldBeTrue();
        result.Reachability.Count.ShouldBe(3);

        Reach("app", "db").CanCommunicate.ShouldBeTrue();
        Reach("app", "proxy").CanCommunicate.ShouldBeTrue();
        Reach("db", "proxy").CanCommunicate.ShouldBeFalse();

        ReachabilityPair Reach(string first, string second) =>
            result.Reachability.Single(pair => pair.ServiceA == first && pair.ServiceB == second);
    }

    [Fact]
    public void ASingleService_HasNoPairsToReport()
    {
        SimulationResult result = TopologySimulator.Simulate(
            Topologies.With(services: [new ContainerService { Name = "api", Image = "api:1" }]));

        result.Reachability.ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();
    }
}
