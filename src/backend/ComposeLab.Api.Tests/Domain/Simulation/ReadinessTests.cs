using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

public sealed class ReadinessTests
{
    [Fact]
    public void WaitingForStart_TeachesThatStartedIsNotReady()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceStarted, healthCheck: null);

        SimulationIssue issue = result.Only(SimulationIssueCode.OrderingIsNotReadiness);

        issue.Severity.ShouldBe(SimulationSeverity.Information);
        issue.Why.ShouldContain("ready");
        issue.SuggestedFix.ShouldContain("retry");
        result.Has(SimulationIssueCode.HealthCheckAvailable).ShouldBeFalse();
    }

    [Fact]
    public void WaitingForStartWhenAHealthCheckExists_SuggestsUsingIt()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceStarted, HealthCheckDeclaration.Shell("pg_isready"));

        result.Only(SimulationIssueCode.HealthCheckAvailable)
            .SuggestedFix.ShouldContain("service_healthy");

        result.Coded(SimulationIssueCode.HealthCheckAvailable)[0].Severity.ShouldBe(SimulationSeverity.Warning);
    }

    [Fact]
    public void WaitingForHealthWithAnEnabledHealthCheck_RaisesNothing()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceHealthy, HealthCheckDeclaration.Shell("pg_isready"));

        result.Issues.ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// A health condition with no healthcheck in the Compose file is not invalid Compose: the image may
    /// declare <c>HEALTHCHECK</c> in its Dockerfile. ComposeLab reports that it cannot verify the
    /// condition, and must not fail the run over it.
    /// </summary>
    [Fact]
    public void WaitingForHealthWithNoDeclaredHealthCheck_IsUnverifiableRatherThanInvalid()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceHealthy, healthCheck: null);

        SimulationIssue issue = result.Only(SimulationIssueCode.HealthConditionUnverifiable);

        issue.Severity.ShouldBe(SimulationSeverity.Warning);
        issue.Why.ShouldContain("Dockerfile");
        issue.Why.ShouldContain("not necessarily wrong");

        result.Errors().ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void WaitingForHealthWhenTheHealthCheckIsDisabled_IsAWarning()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceHealthy, HealthCheckDeclaration.Disabled);

        SimulationIssue issue = result.Only(SimulationIssueCode.HealthConditionDisabled);

        issue.Severity.ShouldBe(SimulationSeverity.Warning);
        issue.WhatHappened.ShouldContain("disabled");
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void TheTimelineStatesThatComposeLabDoesNotRunTheCheck()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceHealthy, HealthCheckDeclaration.Shell("pg_isready"));

        result.Events
            .Single(item => item.Code == SimulationEventCode.DependencySatisfied)
            .Message.ShouldContain("does not run the check");
    }

    [Fact]
    public void TheTimelineNeverInventsHealthCheckAttempts()
    {
        SimulationResult result = Simulate(DependencyCondition.ServiceHealthy, HealthCheckDeclaration.Shell("pg_isready"));

        foreach (SimulationEvent item in result.Events)
        {
            item.Message.ShouldNotContain("attempt");
            item.Message.ShouldNotContain("retry");
        }
    }

    private static SimulationResult Simulate(
        DependencyCondition condition,
        HealthCheckDeclaration? healthCheck) =>
        TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Dependencies = [new ServiceDependency("database", condition)]
                },
                new ContainerService
                {
                    Name = "database",
                    Image = "db:1",
                    HealthCheck = healthCheck
                }
            ]));
}
