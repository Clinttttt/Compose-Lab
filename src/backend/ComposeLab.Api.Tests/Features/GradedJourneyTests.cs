using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Tests.TestSupport;
using GeneratedComposeResponse = ComposeLab.Api.Features.Topology.GenerateCompose.Response;
using ParseComposeResponse = ComposeLab.Api.Features.Topology.ParseCompose.Response;
using ProjectResponse = ComposeLab.Api.Features.Projects.GetById.Response;
using SimulateResponse = ComposeLab.Api.Features.Topology.Simulate.Response;
using ValidateResponse = ComposeLab.Api.Features.Topology.Validate.Response;

namespace ComposeLab.Api.Tests.Features;

/// <summary>
/// The graded journey from concept doc §64, walked end to end against the real API and a real
/// PostgreSQL.
/// </summary>
/// <remarks>
/// Every other test checks one rule in isolation. This one checks that the sequence a learner is
/// actually shown holds together: build, generate, read back, simulate, break it deliberately, get an
/// explanation, repair it, and rerun clean — with a save at each end. If this passes, the demo works;
/// if it fails, something that passes in isolation has stopped composing.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class GradedJourneyTests(DatabaseFixture fixture)
    : DatabaseTestBase(fixture), IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task TheLearnerCanBuildBreakExplainRepairAndSave()
    {
        // 1-6. Build an API and PostgreSQL on a shared network, with a volume and a published API port.
        TopologyDocument built = ReferenceArchitecture("backend", "backend");

        // 1. Create the project. Saving is explicit and comes first, as it does in the walkthrough.
        Guid projectId = await CreateProject("Web API and PostgreSQL", built);

        // 7. The architecture is sound before anything is run.
        ValidateResponse validated = await Post<ValidateResponse>("/api/topology/validate", built);
        validated.IsValid.ShouldBeTrue();
        validated.IsComplete.ShouldBeTrue();

        // 7. Valid YAML is generated, and it carries the map back to the elements that produced it.
        GeneratedComposeResponse generated = await Post<GeneratedComposeResponse>(
            "/api/compose/generate",
            built);

        generated.Yaml.ShouldContain("services:");
        generated.Yaml.ShouldContain("- \"8080:8080\"");
        generated.Yaml.ShouldContain("postgres-data:/var/lib/postgresql");
        generated.Provenance.ShouldContain(entry =>
            entry.Element.Kind == "service" && entry.Element.Name == "api");

        // 8-9. Editing the YAML and applying it: what was written can be read back, unchanged.
        ParseComposeResponse reread = await Post<ParseComposeResponse>(
            "/api/compose/parse",
            new { yaml = generated.Yaml });

        reread.CanApply.ShouldBeTrue();
        reread.Findings.ShouldBeEmpty();
        reread.Topology.ShouldNotBeNull();
        reread.Topology.Services.Select(service => service.Name).ShouldBe(["api", "database"]);

        // Regenerating from the parsed topology produces the same file, so applying an untouched
        // document changes nothing.
        GeneratedComposeResponse regenerated = await Post<GeneratedComposeResponse>(
            "/api/compose/generate",
            reread.Topology);

        regenerated.Yaml.ShouldBe(generated.Yaml);

        // 10. Simulate the working architecture.
        SimulateResponse working = await Post<SimulateResponse>("/api/topology/simulate", built);

        working.Succeeded.ShouldBeTrue();
        working.Completed.ShouldBeTrue();
        working.StartOrder.ShouldBe(["database", "api"]);
        working.Reachability.ShouldHaveSingleItem().CanCommunicate.ShouldBeTrue();

        // 11. Break it on purpose: the two services end up on separate networks.
        TopologyDocument broken = ReferenceArchitecture("frontend", "backend");

        SimulateResponse failed = await Post<SimulateResponse>("/api/topology/simulate", broken);

        failed.Succeeded.ShouldBeFalse();
        failed.Completed.ShouldBeTrue();

        // 12. The explanation says what happened, why, what it means, and what to try.
        var unreachable = failed.Issues.Single(issue => issue.Code == "network.unreachable");

        unreachable.Severity.ShouldBe("error");
        unreachable.WhatHappened.ShouldBe("'api' cannot reach 'database'.");
        unreachable.Why.ShouldContain("frontend");
        unreachable.Why.ShouldContain("backend");
        unreachable.ArchitectureBehavior.ShouldContain("ConnectionStrings__Database");
        unreachable.SuggestedFix.ShouldContain("shared network");
        unreachable.Elements.ShouldContain(element =>
            element.Kind == "environment_variable" && element.OwnerService == "api");

        // A broken architecture is still the learner's work, and still saveable.
        await SaveProject(projectId, "Web API and PostgreSQL (broken)", broken);

        // 13. Repair it by putting both services back on one network.
        TopologyDocument repaired = ReferenceArchitecture("backend", "backend");

        // 14. Rerun, successfully.
        SimulateResponse rerun = await Post<SimulateResponse>("/api/topology/simulate", repaired);

        rerun.Succeeded.ShouldBeTrue();
        rerun.Issues.ShouldNotContain(issue => issue.Code == "network.unreachable");
        rerun.Events.ShouldNotBeEmpty();

        // The repaired architecture is saved, and comes back exactly as saved.
        await SaveProject(projectId, "Web API and PostgreSQL", repaired);

        ProjectResponse reopened = await Get<ProjectResponse>($"/api/projects/{projectId}");

        reopened.Name.ShouldBe("Web API and PostgreSQL");
        reopened.TopologySchemaVersion.ShouldBe(TopologySchema.CurrentVersion);
        reopened.Topology.Services.Select(service => service.Name).ShouldBe(["api", "database"]);
        reopened.Topology.Services.ShouldAllBe(service => service.Networks.Contains("backend"));
        reopened.Topology.Networks.ShouldHaveSingleItem().Name.ShouldBe("backend");
        reopened.Topology.Volumes.ShouldHaveSingleItem().Name.ShouldBe("postgres-data");
    }

    /// <summary>
    /// The architecture the walkthrough builds. The connection string is what makes the API's intent to
    /// reach the database explicit, which is what turns a split network into a reported failure rather
    /// than two services that simply do not talk.
    /// </summary>
    private static TopologyDocument ReferenceArchitecture(string apiNetwork, string databaseNetwork) =>
        new()
        {
            Services =
            [
                new ServiceDocument
                {
                    Name = "api",
                    Build = "./Api",
                    Ports = [new PortDocument { HostPort = 8080, ContainerPort = 8080 }],
                    Networks = [apiNetwork],
                    DependsOn = [new DependencyDocument { Service = "database", Condition = "service_started" }],
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ConnectionStrings__Database"] = "Host=database;Database=app"
                    }
                },
                new ServiceDocument
                {
                    Name = "database",
                    Image = "postgres:18",
                    // Unpublished: the API reaches it by name on a shared network.
                    Ports = [],
                    Networks = [databaseNetwork],
                    Volumes =
                    [
                        new VolumeMountDocument { Volume = "postgres-data", Path = "/var/lib/postgresql" }
                    ]
                }
            ],
            Networks =
            [
                .. new[] { apiNetwork, databaseNetwork }
                    .Distinct(StringComparer.Ordinal)
                    .Select(name => new NetworkDocument { Name = name })
            ],
            Volumes = [new VolumeDocument { Name = "postgres-data" }]
        };

    private async Task<Guid> CreateProject(string name, TopologyDocument topology)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/projects",
            new { name, topology },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<Guid>(Token);
    }

    private async Task SaveProject(Guid id, string name, TopologyDocument topology)
    {
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            $"/api/projects/{id}",
            new { name, topology },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<TResponse> Post<TResponse>(string route, object payload)
        where TResponse : class
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<TResponse>(Token)).ShouldNotBeNull();
    }

    private async Task<TResponse> Get<TResponse>(string route)
        where TResponse : class
    {
        HttpResponseMessage response = await Client.GetAsync(route, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<TResponse>(Token)).ShouldNotBeNull();
    }
}
