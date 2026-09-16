namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// The catalog of every finding ComposeLab can report. Each code's explanation text lives with the
/// rule family that produces it, so a rule and the teaching content it emits cannot drift apart.
/// </summary>
public sealed record SimulationIssueCode
{
    private SimulationIssueCode(string value) => Value = value;

    public string Value { get; }

    // Structure — the architecture could not be simulated at all.
    public static readonly SimulationIssueCode DuplicateServiceName = new("structure.duplicate_service_name");

    public static readonly SimulationIssueCode ServiceWithoutImageOrBuild = new("structure.service_without_image_or_build");

    public static readonly SimulationIssueCode UndeclaredNetwork = new("structure.undeclared_network");

    public static readonly SimulationIssueCode UndeclaredVolume = new("structure.undeclared_volume");

    public static readonly SimulationIssueCode UnknownDependency = new("structure.unknown_dependency");

    public static readonly SimulationIssueCode SelfDependency = new("structure.self_dependency");

    public static readonly SimulationIssueCode DependencyCycle = new("structure.dependency_cycle");

    // Ports.
    public static readonly SimulationIssueCode HostPortCollision = new("port.host_collision");

    // Reachability.
    public static readonly SimulationIssueCode NetworkUnreachable = new("network.unreachable");

    // Readiness.
    public static readonly SimulationIssueCode OrderingIsNotReadiness = new("readiness.ordering_is_not_readiness");

    public static readonly SimulationIssueCode HealthCheckAvailable = new("readiness.healthcheck_available");

    public static readonly SimulationIssueCode HealthConditionUnverifiable = new("readiness.health_condition_unverifiable");

    public static readonly SimulationIssueCode HealthConditionDisabled = new("readiness.health_condition_disabled");

    // Persistence and exposure.
    public static readonly SimulationIssueCode MissingPersistentVolume = new("persistence.missing_volume");

    public static readonly SimulationIssueCode OptionalPersistenceNotConfigured = new("persistence.optional_not_configured");

    public static readonly SimulationIssueCode StatefulServicePublished = new("exposure.stateful_service_published");

    public override string ToString() => Value;
}
