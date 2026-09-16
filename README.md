# ComposeLab

Build a container architecture, see the Compose file it produces, and find out what Docker would do
with it — before running anything.

ComposeLab is not a Compose generator with a preview. The point is the correspondence between three
things: what you built, what the YAML says, and why it works or fails. A learner assembles an
architecture, gets a canonical `compose.yaml` from it, selects a service and sees exactly which lines
that service produced, runs a simulation, deliberately breaks the network, and reads an explanation
that names the two networks and the connection string that was supposed to cross them.

The simulation is deterministic and starts no containers. It reads the architecture and reports what
Compose would do with it.

## Prerequisites

- .NET 10 SDK
- Node 24 and npm
- Docker (for the development database, and for the backend's integration tests)

## Run it

```bash
# 1. Development database
docker compose up -d

# 2. Schema
dotnet tool install --global dotnet-ef      # once
dotnet ef database update --project src/backend/ComposeLab.Api

# 3. API — http://localhost:5131
cd src/backend/ComposeLab.Api && dotnet run

# 4. Workspace — http://localhost:4200
cd src/frontend && npm install && npm start
```

The API's development CORS allowlist is `http://localhost:4200`, and the frontend's
`environment.development.ts` points at `http://localhost:5131`. Those two are the only places either
address appears.

The database is published on `127.0.0.1:55432` rather than 5432, because 5432 is often already taken
on a developer machine. Nothing inside the project cares: the container port is unchanged, and
services reach each other by name.

## The demo

This is the walkthrough the project is built around. It takes about three minutes.

1. **Build it.** Components → *API and PostgreSQL*. Four things appear: an API published on host port
   8080, a PostgreSQL that is deliberately *not* published, a `backend` network, and a
   `postgres-data` volume. Point out that the database has no host port and does not need one.
2. **Read the YAML.** The pane on the right is generated from the architecture, continuously. Select
   the `api` service in the diagram and the lines it produced highlight. Click a YAML line and the
   element it belongs to is selected. Neither direction is guessed — the backend returns the line
   ranges along with the file.
3. **Edit the YAML.** Press *Edit YAML*, change something, and note that highlighting switches off
   and says why: the saved line ranges no longer describe what is on screen. Add `restart: always`
   and press *Apply YAML*. It is refused, with the line, the path, and the reason — `restart` is real
   Compose that ComposeLab does not model, so applying it would silently drop it. Remove it and Apply
   succeeds.
4. **Simulate.** Press *Run simulation*. The console shows the ordered steps: the network created, the
   volume created, the database attached and mounted, the API attached, the port published. Nothing
   is timestamped, because nothing here is timed.
5. **Break it.** Select `api`, uncheck `backend`, add a second network and attach the API to that
   instead. Run the simulation again. It fails with `network.unreachable`, and the explanation names
   both networks, states that the connection string in `ConnectionStrings__Database` cannot resolve
   `database`, and says the fix is one shared network. Click the chips to jump to the elements
   involved.
6. **Repair and rerun.** Put the API back on `backend`. Simulate again: clean.
7. **Save.** *Save project*. Reopen it from the dropdown and the architecture comes back exactly as
   saved.

Two things worth saying out loud during the demo. Delete every network and simulate again: the
services still reach each other, because Compose supplies a `default` network — the diagram draws it
as a dashed band marked *supplied by Compose*, and the generated file does not mention it, because it
is Compose's behaviour rather than your configuration. And a broken architecture saves fine: storage
keeps your work, the engine explains it.

## Verify

```bash
# Backend — formatting, build with warnings as errors, and tests including a real PostgreSQL
cd src/backend
dotnet format ComposeLab.slnx --verify-no-changes
dotnet build ComposeLab.slnx
dotnet test --solution ComposeLab.slnx

# Frontend
cd src/frontend
npm run format:check
npm test
npm run build
```

CI runs exactly these, in two jobs.

## What is where

```text
src/backend/ComposeLab.Api/
├── Domain/
│   ├── Topology/          the model, and the authored document that is stored and sent
│   ├── Simulation/        the deterministic engine and its rule catalog
│   └── Compose/           YAML generation with provenance, and parsing with classification
├── Features/              one folder per use case
│   ├── Topology/          simulate, validate, generate, parse — all stateless
│   └── Projects/          create, list, get, update
└── Infrastructure/        EF Core, the jsonb mapping, shared request validation

src/frontend/src/app/
├── core/                  the two interceptors and a transport-error notifier
├── shared/                icons, not-found
└── features/workspace/    one store, five panels
```

Nine routes: `POST /api/topology/simulate`, `POST /api/topology/validate`,
`POST /api/compose/generate`, `POST /api/compose/parse`, `POST /api/projects`, `GET /api/projects`,
`GET /api/projects/{id}`, `PUT /api/projects/{id}`, and `GET /health`.

The engine routes are stateless. None of them needs a saved project, and the four project routes are
the only ones that touch storage.

## Deliberate limits

These are decisions, not gaps waiting to be filled.

- **No authentication.** Every route is open, so anyone who can reach the API can read and overwrite
  any project. Fine for a local machine and a classroom; the first thing to change before this is
  reachable from anywhere else.
- **A narrow Compose subset.** `image`, `build` (short form), `ports`, `environment`, `volumes`
  (named only), `networks`, `depends_on`, and `healthcheck`. Anything else is reported rather than
  ignored: real Compose that is not modelled is told apart from a key that is not Compose at all,
  which is how `enviroment` gets caught. Applying a file is all-or-nothing, so nothing is ever
  silently dropped.
- **Health is declared, never observed.** ComposeLab records which form a healthcheck test takes and
  never runs it, so it does not model intervals, retries, or timeouts.
- **No real Docker.** The simulator interprets the architecture. Presenting simulated output as
  actual container behaviour is the one thing it must never do.
- **The stateful-image catalog is incomplete.** Persistence advice covers the common data stores; an
  unrecognised image gets silence rather than a guess.
- **No autosave, no history, no ownership.** Saving is explicit. Undo and redo cover the topology
  in-session only, and are cleared when a project is opened.
