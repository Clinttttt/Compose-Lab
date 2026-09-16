using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

public sealed class PersistenceTests
{
    [Fact]
    public void DurableStoreWithNoVolume_IsAWarning()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [new ContainerService { Name = "database", Image = Topologies.PostgresImage }]));

        SimulationIssue issue = result.Only(SimulationIssueCode.MissingPersistentVolume);

        issue.Severity.ShouldBe(SimulationSeverity.Warning);
        issue.WhatHappened.ShouldContain(Topologies.PostgresDataPath);
        issue.WhatHappened.ShouldContain("PostgreSQL");
        issue.SuggestedFix.ShouldContain("named volume");

        // A warning is not a failure: the architecture runs, it just loses data.
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void VolumeMountedAtTheDataPath_SatisfiesPersistence()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Volumes = [new VolumeMount("data", Topologies.PostgresDataPath)]
                }
            ],
            volumes: ["data"]));

        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeFalse();
    }

    [Fact]
    public void VolumeMountedAtAParentDirectory_AlsoSatisfiesPersistence()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Volumes = [new VolumeMount("data", "/var/lib/postgresql")]
                }
            ],
            volumes: ["data"]));

        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeFalse();
    }

    [Fact]
    public void AnUnrelatedMountPath_DoesNotSatisfyPersistence()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Volumes = [new VolumeMount("logs", "/var/log")]
                }
            ],
            volumes: ["logs"]));

        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeTrue();
    }

    /// <summary>
    /// Redis is often deliberately disposable, so the same missing volume is a note rather than a
    /// warning. Treating a cache like a database would train learners to ignore the message.
    /// </summary>
    [Fact]
    public void OptionalPersistenceStoreWithNoVolume_IsInformationOnly()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [new ContainerService { Name = "cache", Image = "redis:8" }]));

        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeFalse();

        SimulationIssue issue = result.Only(SimulationIssueCode.OptionalPersistenceNotConfigured);

        issue.Severity.ShouldBe(SimulationSeverity.Information);
        issue.SuggestedFix.ShouldContain("no change is needed");
    }

    [Fact]
    public void PublishingADataStore_IsAWarningAboutExposure()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "database",
                    Image = Topologies.PostgresImage,
                    Ports = [new PortMapping(5432, 5432)],
                    Volumes = [new VolumeMount("data", Topologies.PostgresDataPath)]
                }
            ],
            volumes: ["data"]));

        SimulationIssue issue = result.Only(SimulationIssueCode.StatefulServicePublished);

        issue.Severity.ShouldBe(SimulationSeverity.Warning);
        issue.Why.ShouldContain("container port");
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void AnUnpublishedDataStore_RaisesNoExposureWarning()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.ApiWithPostgres());

        result.Has(SimulationIssueCode.StatefulServicePublished).ShouldBeFalse();
        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeFalse();
    }

    /// <summary>The catalog is intentionally incomplete, so an unrecognized image produces silence.</summary>
    [Fact]
    public void AnUnrecognizedImage_ProducesNoPersistenceFinding()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [new ContainerService { Name = "api", Image = "mycompany/api:latest" }]));

        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeFalse();
        result.Has(SimulationIssueCode.OptionalPersistenceNotConfigured).ShouldBeFalse();
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("postgres:18")]
    [InlineData("postgres:18-alpine")]
    [InlineData("docker.io/library/postgres:18")]
    [InlineData("localhost:5000/postgres:18")]
    [InlineData("POSTGRES:18")]
    public void ImageReferencesAreMatchedRegardlessOfRegistryTagOrCasing(string image)
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services: [new ContainerService { Name = "database", Image = image }]));

        result.Has(SimulationIssueCode.MissingPersistentVolume).ShouldBeTrue();
    }

    [Fact]
    public void SqlServerIsRecognizedThroughItsRegistryPath()
    {
        SimulationResult result = TopologySimulator.Simulate(Topologies.With(
            services:
            [
                new ContainerService { Name = "database", Image = "mcr.microsoft.com/mssql/server:2022-latest" }
            ]));

        result.Only(SimulationIssueCode.MissingPersistentVolume).WhatHappened.ShouldContain("SQL Server");
    }
}
