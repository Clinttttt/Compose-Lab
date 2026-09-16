using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

public sealed class TopologyValidatorTests
{
    /// <summary>
    /// The property that matters most about having two entry points: they read the same rule catalog, so an
    /// architecture cannot be sound according to one and broken according to the other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Architectures))]
    public void ValidationAndSimulationAlwaysAgree(ApplicationTopology topology)
    {
        ValidationReport report = TopologyValidator.Validate(topology);
        SimulationResult simulation = TopologySimulator.Simulate(topology);

        report.IsValid.ShouldBe(simulation.Succeeded);
        report.IsComplete.ShouldBe(simulation.Completed);

        report.Issues.Select(issue => issue.Code.Value).OrderBy(code => code, StringComparer.Ordinal)
            .ShouldBe(simulation.Issues.Select(issue => issue.Code.Value)
                .OrderBy(code => code, StringComparer.Ordinal));
    }

    public static TheoryData<ApplicationTopology> Architectures() =>
    [
        Topologies.ApiWithPostgres(),
        Topologies.ApiWithPostgres(apiNetwork: "frontend", databaseNetwork: "backend"),
        Topologies.With(services: [new ContainerService { Name = "api", Image = "api:1" }]),
        Topologies.With(services: [new ContainerService { Name = "database", Image = "postgres:18" }]),
        Topologies.With(
            services:
            [
                new ContainerService { Name = "api", Image = "api:1" },
                new ContainerService { Name = "api", Image = "api:2" }
            ]),
        Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Ports = [new PortMapping(8080, 8080)]
                },
                new ContainerService
                {
                    Name = "web",
                    Image = "web:1",
                    Ports = [new PortMapping(8080, 80)]
                }
            ]),
        Topologies.With(services: [new ContainerService { Name = "api" }])
    ];

    [Fact]
    public void AValidArchitectureReportsNoErrors()
    {
        ValidationReport report = TopologyValidator.Validate(Topologies.ApiWithPostgres());

        report.IsValid.ShouldBeTrue();
        report.IsComplete.ShouldBeTrue();
        report.Issues.ShouldNotContain(issue => issue.Severity == SimulationSeverity.Error);
    }

    [Fact]
    public void WarningsDoNotMakeAnArchitectureInvalid()
    {
        ValidationReport report = TopologyValidator.Validate(Topologies.With(
            services: [new ContainerService { Name = "database", Image = "postgres:18" }]));

        report.Issues.ShouldContain(issue => issue.Code == SimulationIssueCode.MissingPersistentVolume);
        report.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// When structure is broken the advisory rules would be reasoning about a topology that cannot exist, so
    /// the check stops and says the list is not the whole story.
    /// </summary>
    [Fact]
    public void StructuralFaultsSuppressTheAdvisoryRules()
    {
        ValidationReport report = TopologyValidator.Validate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "database",
                    Image = "postgres:18",
                    Networks = [Topologies.On("nowhere")]
                }
            ]));

        report.IsValid.ShouldBeFalse();
        report.IsComplete.ShouldBeFalse();
        report.Issues.ShouldHaveSingleItem().Code.ShouldBe(SimulationIssueCode.UndeclaredNetwork);

        // The missing volume on postgres is real, but it is not reported until the architecture can run.
        report.Issues.ShouldNotContain(issue => issue.Code == SimulationIssueCode.MissingPersistentVolume);
    }

    [Fact]
    public void ValidationFindsTheSameUnreachableConnectionTheSimulatorDoes()
    {
        ValidationReport report = TopologyValidator.Validate(
            Topologies.ApiWithPostgres(apiNetwork: "frontend", databaseNetwork: "backend"));

        report.IsValid.ShouldBeFalse();
        report.IsComplete.ShouldBeTrue();

        SimulationIssue issue = report.Issues
            .Single(item => item.Code == SimulationIssueCode.NetworkUnreachable);

        issue.WhatHappened.ShouldBe("'api' cannot reach 'database'.");
        issue.SuggestedFix.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AnEmptyArchitectureIsValid()
    {
        ValidationReport report = TopologyValidator.Validate(Topologies.With());

        report.IsValid.ShouldBeTrue();
        report.IsComplete.ShouldBeTrue();
        report.Issues.ShouldBeEmpty();
    }
}
