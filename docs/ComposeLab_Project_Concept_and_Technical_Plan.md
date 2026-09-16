# ComposeLab

## Interactive Containerization Architecture Simulator and Docker Compose Learning Platform

**Proposed Technology Stack:** ASP.NET Core, Blazor, .NET, YAML parsing/generation, optional Docker Engine integration  
**Primary Purpose:** Learn containerization by designing, simulating, validating, and generating Docker Compose architectures  
**Project Type:** Developer education platform / DevOps learning simulator / container architecture builder

---

# 1. Project Overview

**ComposeLab** is an interactive learning and development platform designed to teach containerization concepts through practical experimentation.

Instead of requiring users to memorize Docker Compose YAML syntax first, ComposeLab teaches the architecture behind the YAML.

The platform allows users to:

- visually create containerized application architectures;
- write or edit `compose.yaml` directly;
- keep the visual architecture and YAML synchronized;
- simulate how services, networks, ports, volumes, dependencies, and health checks behave;
- detect architectural and configuration problems;
- explain why a configuration works or fails;
- generate valid Docker Compose files;
- optionally run the architecture against a real Docker Engine in a later phase.

The guiding principle of the platform is:

> **Do not teach the YAML first. Teach the architecture that produces the YAML.**

ComposeLab is therefore not intended to be only a Docker Compose generator. It is intended to become an interactive containerization laboratory where users learn by building, making mistakes, observing consequences, and correcting their architecture.

---

# 2. Core Philosophy

ComposeLab follows the idea:

> **To learn is to build. To build is to experiment. To experiment is to understand.**

A common beginner experience with Docker is:

1. copy a YAML file;
2. run `docker compose up`;
3. receive an error;
4. search for the error;
5. modify the file;
6. repeat until it works.

This often teaches syntax without developing a strong mental model of:

- containers;
- images;
- internal and external ports;
- service-to-service communication;
- Docker networks;
- persistent volumes;
- service dependencies;
- environment variables;
- health checks;
- container lifecycle.

ComposeLab changes the learning process into:

```text
Design
  ↓
Predict
  ↓
Simulate
  ↓
Observe
  ↓
Understand
  ↓
Generate
  ↓
Run
```

The user should understand **why** a Compose configuration works before relying on Docker itself to reveal the answer.

---

# 3. Problem Statement

Containerization is often difficult for beginners because several concepts are introduced at the same time.

For example, a learner may see:

```yaml
services:
  api:
    ports:
      - "8080:8080"
    networks:
      - backend

  database:
    image: postgres
    networks:
      - backend
```

without immediately understanding:

- what `services` represent;
- why two port numbers are present;
- what `backend` actually does;
- how the API finds the database;
- whether PostgreSQL must be exposed to the host;
- what happens when a container is deleted;
- why application data disappears without persistent storage.

Existing tools are typically focused on running and managing containers rather than teaching these relationships step by step.

ComposeLab addresses this gap through a visual and interactive simulation environment.

---

# 4. Proposed Solution

ComposeLab provides a workspace where users can construct and test a containerized application architecture before running real containers.

The central architecture is:

```text
┌──────────────────┐
│   Visual Editor  │
└────────┬─────────┘
         │
         │
         ▼
┌──────────────────┐
│ ApplicationModel │
└────────┬─────────┘
         ▲
         │
┌────────┴─────────┐
│   YAML Editor    │
└──────────────────┘
```

The `ApplicationModel` is the source of truth.

From this model, ComposeLab can:

```text
ApplicationModel
      │
      ├── Visualize architecture
      ├── Validate configuration
      ├── Simulate behavior
      ├── Explain mistakes
      └── Generate compose.yaml
```

This allows the user to work in either direction:

```text
Visual Design → ApplicationModel → YAML
```

or:

```text
YAML → Parser → ApplicationModel → Visual Design
```

---

# 5. Main Environment

The main ComposeLab environment should resemble a lightweight developer IDE combined with a Docker laboratory.

A recommended layout is:

```text
┌──────────────────────────────────────────────────────────────┐
│ ComposeLab                                                   │
│ Project: Web API + PostgreSQL                                │
├───────────────┬───────────────────────────────┬──────────────┤
│ COMPONENTS    │                               │ INSPECTOR    │
│               │        WORKSPACE              │              │
│ ASP.NET API   │                               │ Service      │
│ PostgreSQL    │      [ ASP.NET API ]          │ Image        │
│ Redis         │             │                 │ Ports        │
│ RabbitMQ      │          backend              │ Networks     │
│ Network       │             │                 │ Volumes      │
│ Volume        │       [ PostgreSQL ]          │ Depends On   │
│ Worker        │             │                 │ Environment  │
│               │          pg-data              │ Healthcheck  │
├───────────────┴───────────────────────────────┴──────────────┤
│ Visual | YAML | Split | Simulate | Validate | Generate      │
├──────────────────────────────────────────────────────────────┤
│ SIMULATION CONSOLE                                           │
│ ✓ backend network created                                    │
│ ✓ database started                                           │
│ ✓ api attached to backend                                    │
│ ✓ api can resolve database                                   │
└──────────────────────────────────────────────────────────────┘
```

---

# 6. Workspace Modes

ComposeLab should provide three primary editing modes.

## 6.1 Visual Build Mode

This mode is intended primarily for beginners.

The user adds components such as:

- ASP.NET Core API;
- frontend;
- PostgreSQL;
- SQL Server;
- Redis;
- RabbitMQ;
- worker service;
- generic Docker image;
- Docker network;
- named volume.

The user then configures those components through an inspector panel.

Example:

```text
ASP.NET API
│
├── Image: my-api
├── Container Port: 8080
├── Host Port: 5000
├── Network: backend
└── Depends On: database
```

Changes made visually update the underlying application model and regenerate the corresponding Compose configuration.

---

# 7. YAML Editor Mode

Advanced users can directly write Docker Compose YAML.

Example:

```yaml
services:
  api:
    build: ./Api
    ports:
      - "8080:8080"
    depends_on:
      - database
    networks:
      - backend

  database:
    image: postgres
    networks:
      - backend
    volumes:
      - postgres-data:/var/lib/postgresql/data

networks:
  backend:

volumes:
  postgres-data:
```

ComposeLab parses the YAML and updates the internal application model.

The visual workspace then reflects the configuration.

---

# 8. Split View Mode

This should become one of the signature features of ComposeLab.

Example:

```text
VISUAL ARCHITECTURE                 YAML

[ ASP.NET API ]                     services:
       │                              api:
    backend                              ports:
       │                                   - "8080:8080"
[ PostgreSQL ]                          networks:
       │                                   - backend
   pg-data
```

The purpose is educational.

The learner can immediately see:

> “This line in YAML creates this architectural relationship.”

For example, selecting:

```yaml
networks:
  - backend
```

could highlight the corresponding network connection in the visual workspace.

Similarly, selecting a network line in the diagram could highlight the YAML responsible for it.

---

# 9. Simulation Engine

The simulation engine is the central educational feature of ComposeLab.

The initial simulator does **not** need to start real Docker containers.

Instead, it interprets the application model using predefined rules based on containerization concepts.

Architecture:

```text
compose.yaml
      ↓
 YAML Parser
      ↓
ApplicationModel
      ↓
Simulation Engine
      ↓
Simulation Events
      ↓
Visualization + Explanation
```

Example output:

```text
Starting simulation...

✓ Parsed compose.yaml
✓ Created network "backend"
✓ Created volume "postgres-data"

Starting database...
✓ database attached to backend
✓ postgres-data mounted
✓ database running

Starting api...
✓ api attached to backend
✓ api can resolve "database"
✓ host:8080 forwards to api:8080

Simulation completed successfully.
```

---

# 10. Concepts to Simulate

The MVP should focus on a manageable set of important containerization concepts.

## 10.1 Services

Users should understand that a Compose service describes a containerized workload.

Examples:

- API;
- database;
- cache;
- queue;
- frontend;
- worker.

---

## 10.2 Images

The simulator should explain the relationship:

```text
Docker Image
     ↓
Container Instance
```

Example:

```text
postgres:18
     ↓
PostgreSQL container
```

---

## 10.3 Port Mapping

The simulator should visually distinguish:

```text
HOST PORT          CONTAINER PORT

localhost:5000  →  api:8080
```

This teaches an important beginner concept:

```yaml
ports:
  - "5000:8080"
```

means:

```text
HOST:5000 → CONTAINER:8080
```

---

## 10.4 Networks

A simplified communication rule can be implemented:

```text
Service A and Service B
        ↓
Do they share a network?
        ↓
YES → communication possible
NO  → communication blocked
```

Example:

```text
API ───── backend ───── PostgreSQL
```

Result:

```text
✓ API can communicate with PostgreSQL.
```

Invalid example:

```text
API ─── api-network

PostgreSQL ─── database-network
```

Result:

```text
✕ Communication failed.

Reason:
API and PostgreSQL do not share a network.
```

---

# 11. Service Discovery Simulation

ComposeLab should demonstrate that services can communicate using service names inside a shared network.

Example:

```text
API
 │
 │ Connection String:
 │ Host=database
 │
 ▼
database
```

The simulator can explain:

```text
✓ "database" can be resolved.

Reason:
Both services are attached to the backend network.
```

---

# 12. Volume Simulation

Volumes are another strong educational feature.

Example:

```text
PostgreSQL
     │
     ▼
/var/lib/postgresql/data
     │
     ▼
postgres-data
```

The learner could simulate deleting the container.

```text
DELETE PostgreSQL CONTAINER

Container
   ✕ removed

Named Volume
   ✓ remains
```

Then:

```text
RECREATE PostgreSQL

New Container
      │
      ▼
postgres-data
      ✓ reattached
```

ComposeLab can explain:

> A named volume exists independently from an individual container and can be reused when a new container is created.

---

# 13. Dependency Simulation

The simulator can model service dependencies.

Example:

```yaml
depends_on:
  - database
```

Visualization:

```text
database
   │
   │ starts first
   ▼
  api
```

A more advanced version could introduce health conditions.

```text
database starting
      ↓
database healthy
      ↓
api starts
```

---

# 14. Health Check Simulation

The simulator should eventually support:

```text
starting
   ↓
healthy
```

or:

```text
starting
   ↓
unhealthy
   ↓
dependent service blocked
```

Example console:

```text
database starting...
database healthcheck attempt 1... failed
database healthcheck attempt 2... failed
database healthcheck attempt 3... passed

✓ database healthy
✓ starting api
```

---

# 15. Environment Variable Simulation

Users should be able to configure:

```yaml
environment:
  ConnectionStrings__Database: Host=database;Database=app
```

ComposeLab should explain:

```text
Environment variable
        ↓
Injected into container
        ↓
Read by ASP.NET Core configuration
```

Sensitive values can later introduce the concepts of secrets and secure configuration.

---

# 16. Failure Simulation

ComposeLab becomes much more valuable when it can intentionally demonstrate failures.

Possible scenarios include:

### Port Collision

```text
API
Host Port: 8080

Frontend
Host Port: 8080
```

Result:

```text
✕ PORT COLLISION

Host port 8080 is already assigned to API.
Frontend cannot bind to the same host port.
```

---

### Missing Shared Network

```text
API              PostgreSQL
 │                    │
network-a          network-b
```

Result:

```text
✕ API cannot reach PostgreSQL.

Reason:
No shared network exists.
```

---

### Missing Persistent Volume

```text
PostgreSQL
     │
     X
No persistent storage
```

Result:

```text
⚠ Persistence warning

Database files exist only inside the container filesystem.
Removing the container may remove the stored database data.
```

---

### Missing Dependency

```text
API starts
   ↓
Database unavailable
   ↓
API connection fails
```

ComposeLab can explain that startup ordering alone does not always guarantee application readiness.

---

# 17. Simulation Console

The lower part of the environment should contain a simulation console.

Example:

```text
SIMULATION — RUN #15

00:00  Parsing compose.yaml
00:01  ✓ Syntax accepted

00:02  Creating network backend
00:03  ✓ backend created

00:04  Creating volume postgres-data
00:05  ✓ volume created

00:06  Starting database
00:07  ✓ database attached to backend
00:08  ✓ volume mounted
00:09  ✓ database running

00:10  Starting api
00:11  ✓ api attached to backend
00:12  ✓ api resolves database
00:13  ✓ port 8080 exposed

RESULT: SUCCESS
```

The console should be understandable rather than attempting to imitate Docker logs exactly.

---

# 18. Inspector Panel

Clicking a component opens its properties.

Example:

```text
ASP.NET API

Name
api

Image
mycompany/api:latest

Container Port
8080

Host Port
5000

Networks
✓ backend

Volumes
None

Dependencies
database

Environment Variables
ASPNETCORE_ENVIRONMENT=Development
```

The same panel can be used to edit the architecture.

---

# 19. Validation Engine

Validation should be separate from simulation.

Possible validation categories:

## Syntax Validation

```text
Is the YAML syntactically valid?
```

## Schema Validation

```text
Are expected Compose properties structured correctly?
```

## Architecture Validation

```text
Are referenced networks declared?
Are referenced volumes declared?
Do duplicate host ports exist?
Does a dependency reference an existing service?
```

## Educational Validation

These are not necessarily invalid Docker configurations but may represent poor or risky design.

Example:

```text
⚠ Database port is exposed to the host.

This may be unnecessary if only the API needs to access PostgreSQL.
```

---

# 20. Explanation Engine

Every problem should include:

1. **What happened**
2. **Why it happened**
3. **How the architecture behaves**
4. **Possible fix**
5. **Relevant YAML**

Example:

```text
PROBLEM

API cannot reach PostgreSQL.

WHY

API belongs to:
- frontend

PostgreSQL belongs to:
- backend

They have no shared network.

FIX

Attach both services to one shared network.

YAML

services:
  api:
    networks:
      - backend

  database:
    networks:
      - backend
```

This is much more educational than displaying:

```text
Invalid network configuration.
```

---

# 21. Challenge Mode

ComposeLab can include structured exercises.

Example:

```text
MISSION 04
Database Persistence

A company reports that its PostgreSQL data disappears whenever
the container is recreated.

Current architecture:

ASP.NET API
     │
 PostgreSQL

Your task:
Make the database data persistent.
```

The learner must add:

```text
Named Volume
```

When correct:

```text
MISSION COMPLETE

You attached persistent storage to PostgreSQL.

Concept learned:
Container filesystem vs persistent volume
```

---

# 22. Possible Challenge Categories

Challenges can be grouped by difficulty.

## Beginner

- expose an API to the host;
- connect API and database;
- persist database data;
- avoid port collisions;
- configure environment variables.

## Intermediate

- private internal database;
- API + Redis architecture;
- worker + RabbitMQ;
- multiple networks;
- dependency ordering;
- health checks.

## Advanced

- reverse proxy;
- frontend + API + database;
- microservice communication;
- multiple isolated networks;
- service resilience;
- restart strategies.

---

# 23. Learning Mode

ComposeLab should avoid large textbook-style lessons.

Instead, concepts should appear contextually.

Example:

The user adds:

```text
postgres-data
```

ComposeLab explains:

> You created a named volume. This storage exists independently of one specific container and can be reattached to another container.

The lesson appears exactly when the learner performs the action.

This creates:

```text
Action
  ↓
Explanation
  ↓
Observation
  ↓
Understanding
```

---

# 24. Core Domain Model

The application should model container architecture independently of YAML.

Possible entities:

```text
ApplicationTopology
│
├── Services
│   ├── ContainerService
│   ├── PortMapping
│   ├── EnvironmentVariable
│   └── ServiceDependency
│
├── Networks
│   └── ContainerNetwork
│
└── Volumes
    └── ContainerVolume
```

Example C# model:

```csharp
public sealed class ApplicationTopology
{
    public List<ContainerService> Services { get; } = [];
    public List<ContainerNetwork> Networks { get; } = [];
    public List<ContainerVolume> Volumes { get; } = [];
}
```

Example service:

```csharp
public sealed class ContainerService
{
    public Guid Id { get; init; }

    public string Name { get; private set; } = string.Empty;

    public string? Image { get; private set; }

    public string? BuildContext { get; private set; }

    public List<PortMapping> Ports { get; } = [];

    public List<string> Networks { get; } = [];

    public List<VolumeMount> Volumes { get; } = [];

    public List<ServiceDependency> Dependencies { get; } = [];

    public Dictionary<string, string> Environment { get; } = [];
}
```

---

# 25. Port Model

```csharp
public sealed record PortMapping(
    int HostPort,
    int ContainerPort);
```

Example:

```text
HostPort      = 5000
ContainerPort = 8080
```

renders as:

```text
localhost:5000 → api:8080
```

---

# 26. Network Model

```csharp
public sealed class ContainerNetwork
{
    public string Name { get; init; } = string.Empty;
}
```

Service:

```csharp
service.Networks.Add("backend");
```

Simulation:

```csharp
bool CanCommunicate(
    ContainerService first,
    ContainerService second)
{
    return first.Networks
        .Intersect(second.Networks)
        .Any();
}
```

This simplified rule provides a useful first simulation model.

---

# 27. Volume Model

```csharp
public sealed class ContainerVolume
{
    public string Name { get; init; } = string.Empty;
}
```

```csharp
public sealed record VolumeMount(
    string VolumeName,
    string ContainerPath);
```

Example:

```text
postgres-data
     ↓
/var/lib/postgresql/data
```

---

# 28. Dependency Model

```csharp
public sealed record ServiceDependency(
    string ServiceName,
    DependencyCondition Condition);
```

Possible conditions:

```csharp
public enum DependencyCondition
{
    Started,
    Healthy
}
```

---

# 29. Simulation Events

Instead of directly displaying strings from the simulation engine, model simulation events.

Example:

```csharp
public abstract record SimulationEvent(
    DateTimeOffset Timestamp);
```

Possible events:

```text
NetworkCreated
VolumeCreated
ServiceStarting
ServiceStarted
ServiceHealthy
ServiceFailed
NetworkAttached
VolumeMounted
PortExposed
DnsResolved
ConnectionSucceeded
ConnectionFailed
ValidationWarning
```

This makes the simulator easier to visualize later.

---

# 30. Simulation Result

Example:

```csharp
public sealed class SimulationResult
{
    public bool Success { get; init; }

    public List<SimulationEvent> Events { get; init; } = [];

    public List<SimulationIssue> Issues { get; init; } = [];
}
```

---

# 31. Suggested .NET Solution Architecture

A practical solution structure could be:

```text
ComposeLab.sln

src/
│
├── ComposeLab.Web
│   ├── Components
│   ├── Pages
│   ├── Workspace
│   ├── Inspector
│   ├── SimulationConsole
│   └── YamlEditor
│
├── ComposeLab.Api
│   ├── Projects
│   ├── Simulations
│   ├── Validation
│   └── Compose
│
├── ComposeLab.Application
│   ├── Projects
│   ├── Topology
│   ├── Simulations
│   ├── Validation
│   └── ComposeGeneration
│
├── ComposeLab.Domain
│   ├── Topology
│   ├── Services
│   ├── Networks
│   ├── Volumes
│   ├── Simulation
│   └── Validation
│
└── ComposeLab.Infrastructure
    ├── Persistence
    ├── Yaml
    └── Docker
```

---

# 32. Recommended Responsibilities

## ComposeLab.Web

Responsible for:

- visual topology editor;
- YAML editor;
- inspector;
- challenge screens;
- learning explanations;
- simulation visualization.

## ComposeLab.Api

Responsible for:

- HTTP endpoints;
- project persistence;
- simulations;
- validation;
- YAML generation;
- future Docker execution.

## ComposeLab.Application

Responsible for:

- use cases;
- commands and queries;
- simulation orchestration;
- YAML conversion workflows;
- validation workflows.

## ComposeLab.Domain

Responsible for:

- application topology;
- container service rules;
- simulation rules;
- network relationships;
- port rules;
- volume relationships.

## ComposeLab.Infrastructure

Responsible for:

- database;
- YAML parser;
- YAML generator;
- filesystem;
- optional Docker Engine integration.

---

# 33. Suggested Technology Stack

## Backend

- .NET
- ASP.NET Core Web API
- FluentValidation
- EF Core
- PostgreSQL

## Frontend

Recommended:

- Blazor Web App

Possible alternatives:

- React + ASP.NET Core API;
- Vue + ASP.NET Core API.

Blazor is especially attractive if the goal is to keep most of the project in C#.

---

# 34. YAML Handling

ComposeLab requires two major YAML operations:

```text
YAML → ApplicationTopology
```

and:

```text
ApplicationTopology → YAML
```

A YAML library can handle serialization and deserialization while ComposeLab handles the interpretation of Docker Compose semantics.

The important design rule is:

> Do not make YAML itself the domain model.

Instead:

```text
YAML
 ↓
DTO / Parser
 ↓
ApplicationTopology
```

and:

```text
ApplicationTopology
 ↓
Compose DTO
 ↓
YAML Serializer
```

---

# 35. Project Persistence

Users should eventually be able to save ComposeLab projects.

Example:

```text
Project

Name:
E-Commerce Demo

Services:
3

Networks:
2

Volumes:
1

Last Simulation:
Successful
```

Possible database entities:

```text
Project
ProjectVersion
ChallengeProgress
SimulationRun
User
```

---

# 36. Project Versioning

A useful later feature is architecture history.

Example:

```text
Version 1
API + PostgreSQL

Version 2
Added Redis

Version 3
Added persistent volume

Version 4
Isolated database network
```

This allows learners to understand how architectures evolve.

---

# 37. MVP Scope

The first version should remain intentionally focused.

## MVP Feature Set

### Project Workspace

- create project;
- rename project;
- save project.

### Visual Components

- generic service;
- ASP.NET Core API template;
- PostgreSQL;
- Redis;
- network;
- volume.

### Editable Properties

- service name;
- image;
- ports;
- networks;
- volumes;
- dependencies;
- environment variables.

### YAML

- generate Compose YAML;
- edit YAML;
- parse YAML;
- synchronize YAML with the visual model.

### Validation

- invalid YAML;
- missing referenced service;
- missing network;
- missing volume;
- duplicate service name;
- duplicate host port.

### Simulation

- service startup;
- network creation;
- network attachment;
- volume creation;
- volume mounting;
- port publishing;
- simplified DNS/service discovery;
- service dependency;
- communication success/failure.

### Explanations

- network mismatch;
- port collision;
- missing volume;
- invalid dependency;
- unnecessary database exposure warning.

---

# 38. Features That Should NOT Be in the First MVP

Avoid attempting to simulate every Docker feature immediately.

Do not make the first release depend on:

- Kubernetes;
- Docker Swarm;
- full Linux namespace simulation;
- real packet routing;
- real filesystem emulation;
- container security profiles;
- complete Compose specification coverage;
- production cluster orchestration;
- complex secret management;
- remote Docker hosts;
- full Docker Desktop replacement.

These could dramatically increase project scope.

---

# 39. Phase 1 — Conceptual Simulator

```text
User
 ↓
Visual/YAML Editor
 ↓
ApplicationTopology
 ↓
Rules Engine
 ↓
Simulation
```

No Docker installation is required.

Advantages:

- safe;
- deterministic;
- easy to test;
- works on any server;
- inexpensive;
- ideal for education.

---

# 40. Phase 2 — Rich Learning Platform

Add:

- challenges;
- guided lessons;
- architecture comparisons;
- simulation history;
- hints;
- progress tracking;
- difficulty levels.

---

# 41. Phase 3 — Real Docker Mode

Eventually add:

```text
RUN IN DOCKER
```

Architecture:

```text
ComposeLab
    │
    ▼
Docker Integration Layer
    │
    ▼
Docker Engine
    │
    ├── Containers
    ├── Networks
    ├── Volumes
    └── Images
```

ComposeLab can compare:

```text
EXPECTED                  ACTUAL

API Running               API Running       ✓
DB Running                DB Running        ✓
Redis Running             Redis Failed      ✕

Expected network          backend
Actual network            backend           ✓
```

This transforms ComposeLab from a purely educational simulator into a developer experimentation tool.

---

# 42. Security Considerations for Real Docker Mode

Real Docker execution must be treated as a privileged capability.

Important concerns include:

- arbitrary image execution;
- resource exhaustion;
- host filesystem mounts;
- privileged containers;
- Docker socket access;
- malicious Compose configuration;
- unrestricted networks;
- secret exposure.

Therefore:

> Real Docker execution should not be part of the initial public MVP.

A safer future design could use:

- isolated runner machines;
- strict resource limits;
- restricted image sources;
- disabled privileged mode;
- mount restrictions;
- execution time limits;
- per-user sandboxes.

---

# 43. Simulation vs Real Execution

ComposeLab should always distinguish these clearly.

```text
SIMULATION MODE

✓ Safe
✓ No real container
✓ Fast
✓ Educational
✓ Deterministic
```

versus:

```text
REAL DOCKER MODE

✓ Actual containers
✓ Actual networks
✓ Actual images
✓ Actual volumes

⚠ Requires Docker Engine
⚠ Requires stronger security controls
```

Users should never mistake simulation output for actual Docker execution.

---

# 44. Example Learning Scenario

## Scenario

Build an ASP.NET Core API using PostgreSQL.

Requirements:

- API must be reachable from the host;
- PostgreSQL must only communicate internally;
- PostgreSQL data must persist;
- API should wait for the database dependency.

The learner builds:

```text
HOST
 │
 │ localhost:8080
 ▼
ASP.NET API
 │
 │ backend
 ▼
PostgreSQL
 │
 ▼
postgres-data
```

Generated YAML:

```yaml
services:

  api:
    build: ./Api
    ports:
      - "8080:8080"
    networks:
      - backend
    depends_on:
      - database

  database:
    image: postgres
    networks:
      - backend
    volumes:
      - postgres-data:/var/lib/postgresql/data

networks:
  backend:

volumes:
  postgres-data:
```

Simulation:

```text
✓ backend created
✓ postgres-data created
✓ database started
✓ database attached to backend
✓ volume mounted
✓ api started
✓ api attached to backend
✓ api resolves database
✓ API reachable at localhost:8080

Architecture valid.
```

---

# 45. Example Broken Architecture

The user accidentally configures:

```yaml
services:

  api:
    networks:
      - frontend

  database:
    networks:
      - database-network
```

ComposeLab produces:

```text
CONNECTION FAILURE

API
 │
frontend
 │
 X
 │
database-network
 │
PostgreSQL
```

Explanation:

```text
API and PostgreSQL do not share a Docker network.

API networks:
- frontend

PostgreSQL networks:
- database-network

Recommended fix:
Attach both services to one shared network.
```

---

# 46. Example Port Collision

Configuration:

```yaml
services:

  api:
    ports:
      - "8080:8080"

  frontend:
    ports:
      - "8080:80"
```

Simulation:

```text
HOST

Port 8080
   │
   ├── API
   │
   └── Frontend
        ✕

PORT COLLISION
```

Explanation:

```text
Two services are attempting to use the same host port.

Suggested fix:

API
8080:8080

Frontend
3000:80
```

---

# 47. Example Persistence Lesson

Without volume:

```text
PostgreSQL Container
       │
       ▼
Container Filesystem
```

Simulation:

```text
Delete PostgreSQL container
      ↓
Database filesystem removed
```

With volume:

```text
PostgreSQL
    │
    ▼
postgres-data
```

Simulation:

```text
Delete container
   ↓
postgres-data remains

Create new container
   ↓
postgres-data reattached
```

---

# 48. Optional Templates

ComposeLab can provide architecture templates.

Examples:

## ASP.NET + PostgreSQL

```text
API → PostgreSQL
```

## ASP.NET + PostgreSQL + Redis

```text
      ┌→ PostgreSQL
API ──┤
      └→ Redis
```

## Frontend + API + Database

```text
Frontend
   ↓
API
   ↓
PostgreSQL
```

## Worker Architecture

```text
API → RabbitMQ → Worker → PostgreSQL
```

Templates should remain editable.

---

# 49. Potential Dashboard

The project home screen could show:

```text
Your Labs

E-Commerce API
4 services
2 networks
1 volume
Last simulation: Successful

Messaging Lab
3 services
1 network
0 volumes
Last simulation: Failed

PostgreSQL Basics
2 services
1 network
1 volume
Completed challenge
```

---

# 50. Suggested API Endpoints

Possible endpoints:

```text
POST   /api/projects
GET    /api/projects
GET    /api/projects/{id}
PUT    /api/projects/{id}

POST   /api/projects/{id}/parse-compose
POST   /api/projects/{id}/generate-compose
POST   /api/projects/{id}/validate
POST   /api/projects/{id}/simulate

GET    /api/projects/{id}/simulation-runs
GET    /api/challenges
GET    /api/challenges/{id}
```

---

# 51. Example Simulation Command

```csharp
public sealed record SimulateTopologyCommand(
    Guid ProjectId);
```

Handler:

```text
Load project
   ↓
Build topology
   ↓
Validate topology
   ↓
Run simulation
   ↓
Store result
   ↓
Return simulation events
```

---

# 52. Testing Strategy

ComposeLab is highly suitable for automated testing because the core simulation should be deterministic.

Example unit test:

```text
Given:
API belongs to backend
Database belongs to backend

When:
communication is simulated

Then:
connection succeeds
```

Another:

```text
Given:
API belongs to frontend
Database belongs to backend

Then:
connection fails
with NetworkMismatch issue
```

---

# 53. Important Unit Tests

Test:

- shared network communication;
- missing network communication;
- duplicate port detection;
- valid volume mount;
- missing volume reference;
- dependency references;
- YAML serialization;
- YAML parsing;
- round-trip YAML consistency;
- topology validation;
- simulation order.

---

# 54. Round-Trip YAML Testing

An important test:

```text
YAML A
  ↓
Parse
  ↓
ApplicationTopology
  ↓
Generate
  ↓
YAML B
```

Then verify that YAML B represents the same architecture as YAML A.

Formatting does not necessarily have to be identical, but the semantic configuration should be preserved.

---

# 55. UX Principle

Every important Compose concept should have three representations:

```text
1. VISUAL

API ─── backend ─── Database
```

```yaml
2. YAML

networks:
  - backend
```

```text
3. EXPLANATION

Both services share the backend network,
therefore communication is possible.
```

This becomes a major design principle for ComposeLab.

---

# 56. Learning Feedback Principle

Avoid feedback such as:

```text
Invalid configuration.
```

Prefer:

```text
API cannot reach PostgreSQL.

Your API is attached to:
- frontend

Your database is attached to:
- backend

They do not share a network.

Try attaching both services to the same network.
```

The system should teach rather than merely reject.

---

# 57. Feasibility

The project is technically feasible.

The simulator itself does not require virtualization or real container execution.

At its core, the first version consists of:

- a YAML parser;
- a C# application topology model;
- validation rules;
- a deterministic simulation engine;
- a visual editor;
- a YAML generator.

These are manageable components for a .NET application.

The project can therefore begin small and grow gradually.

---

# 58. Why This Project Is Interesting

ComposeLab combines several areas of software engineering:

- ASP.NET Core;
- Blazor;
- parsing;
- serialization;
- domain modeling;
- graph relationships;
- validation;
- event simulation;
- visualization;
- DevOps;
- Docker;
- developer tooling;
- education technology.

It is more technically interesting than a standard CRUD application because the primary domain is the architecture and behavior of containerized systems.

---

# 59. Why It Is More Than a YAML Generator

A basic Compose generator provides:

```text
FORM
 ↓
YAML
```

ComposeLab provides:

```text
                ┌──────────────┐
                │ Visual Build │
                └──────┬───────┘
                       │
                       ▼
                 Application
                    Model
                       ▲
                       │
                ┌──────┴───────┐
                │ YAML Editor  │
                └──────────────┘
                       │
         ┌─────────────┼─────────────┐
         ▼             ▼             ▼
     Validate       Simulate       Explain
         │             │             │
         └─────────────┼─────────────┘
                       ▼
                 Generate YAML
                       │
                       ▼
              Optional Real Docker
```

That is the main distinction.

---

# 60. Possible Future Features

After the core platform is stable, possible extensions include:

- Dockerfile education;
- Dockerfile visualizer;
- image layer explanation;
- build context simulation;
- multi-stage build lessons;
- container resource limits;
- secrets;
- reverse proxies;
- Nginx;
- service scaling concepts;
- Docker logs;
- monitoring;
- architecture sharing;
- classroom mode;
- teacher-created challenges;
- achievement system;
- architecture export;
- downloadable Compose projects;
- integration with GitHub;
- real Docker runner;
- Aspire comparison mode;
- Kubernetes learning path.

---

# 61. Dockerfile Learning Expansion

A future module could teach:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet
WORKDIR /app
COPY .
ENTRYPOINT ["dotnet", "App.dll"]
```

Visually:

```text
Base Image
   ↓
Create /app
   ↓
Copy files
   ↓
Define startup command
   ↓
Final Image
```

This could become:

> ComposeLab Build Studio

while the existing Compose simulator becomes:

> ComposeLab Architecture Studio

---

# 62. Educational Progression

A possible learning path:

```text
LEVEL 1
What is a container?

LEVEL 2
Images and containers

LEVEL 3
Ports

LEVEL 4
Networks

LEVEL 5
Volumes

LEVEL 6
Environment variables

LEVEL 7
Dependencies

LEVEL 8
Health checks

LEVEL 9
Multi-service architecture

LEVEL 10
Real Docker execution
```

---

# 63. Recommended MVP Milestones

## Milestone 1 — Domain Model

Build:

- topology;
- services;
- ports;
- networks;
- volumes;
- dependencies.

Goal:

```text
Architecture can exist entirely in C#.
```

---

## Milestone 2 — YAML Conversion

Implement:

```text
YAML → Topology
Topology → YAML
```

Goal:

```text
Basic Compose configurations can be round-tripped.
```

---

## Milestone 3 — Validation

Implement:

- duplicate ports;
- missing networks;
- missing volumes;
- invalid dependencies.

Goal:

```text
Architecture problems are detected without Docker.
```

---

## Milestone 4 — Simulation Engine

Implement:

```text
Create network
Create volume
Start service
Attach network
Mount volume
Resolve service
Publish port
```

Goal:

```text
A deterministic simulation timeline can be generated.
```

---

## Milestone 5 — Blazor Workspace

Implement:

- service cards;
- network relationships;
- volume relationships;
- inspector.

Goal:

```text
Topology can be edited visually.
```

---

## Milestone 6 — Split View

Synchronize:

```text
Visual ↔ ApplicationModel ↔ YAML
```

Goal:

```text
Visual actions immediately teach corresponding YAML.
```

---

## Milestone 7 — Learning Explanations

Add:

- reasons;
- recommendations;
- contextual lessons.

Goal:

```text
Every important error becomes a learning opportunity.
```

---

## Milestone 8 — Challenges

Create:

- network challenge;
- volume challenge;
- port challenge;
- dependency challenge.

Goal:

```text
Users can practice without creating their own architecture first.
```

---

# 64. Suggested MVP Success Criteria

The MVP can be considered successful when a user can:

1. create a project;
2. add an API;
3. add PostgreSQL;
4. connect both to a shared network;
5. add a PostgreSQL volume;
6. expose an API host port;
7. see valid YAML generated;
8. edit the YAML;
9. see the architecture update;
10. simulate startup;
11. intentionally break a network;
12. receive an understandable explanation;
13. repair the problem;
14. rerun the simulation successfully.

If ComposeLab can do this well, the project's core concept has already been proven.

---

# 65. Proposed Final Project Description

## ComposeLab: Interactive Containerization Architecture Simulator and Docker Compose Learning Platform

ComposeLab is an interactive developer education platform that enables users to design, inspect, simulate, validate, and generate containerized application architectures.

The platform combines a visual architecture editor with a synchronized Docker Compose YAML editor, allowing users to understand how services, networks, ports, volumes, dependencies, environment variables, and health conditions work together.

Unlike traditional configuration generators, ComposeLab does not focus only on producing valid YAML. It creates an educational model of the architecture and simulates the expected behavior of the containerized system before any real Docker containers are started.

Users can experiment with common containerization scenarios, observe service startup sequences, test simulated connectivity, identify network and port conflicts, inspect persistence behavior, and receive explanations about why configurations succeed or fail.

The initial platform operates as a deterministic conceptual simulator, making it safe and accessible even without a Docker installation. A future advanced mode may integrate with Docker Engine to execute architectures using real containers and compare expected simulation results with actual runtime behavior.

ComposeLab is designed around the principle that developers learn containerization more effectively when they can build an architecture, observe its behavior, make mistakes, and understand the consequences rather than memorizing configuration syntax alone.

---

# 66. One-Sentence Vision

> **ComposeLab makes Docker Compose understandable by turning container architecture into something developers can build, see, break, simulate, and learn from.**

---

# 67. Recommended First Build

Start with only this flow:

```text
Create Project
      ↓
Add API
      ↓
Add PostgreSQL
      ↓
Add Network
      ↓
Add Volume
      ↓
Generate YAML
      ↓
Run Simulation
      ↓
Show Architecture Result
```

Do this exceptionally well before expanding into a larger DevOps platform.

The most important part of ComposeLab is not the amount of Docker functionality it supports.

The most important part is the connection between:

```text
WHAT THE USER WRITES

        ↕

WHAT THE ARCHITECTURE LOOKS LIKE

        ↕

WHAT THE CONTAINERS WOULD DO

        ↕

WHY IT WORKS OR FAILS
```

That relationship should remain the core of the project throughout its development.
