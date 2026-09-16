import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { WorkspaceStore } from './workspace-store';
import { ParseComposeResponse, ServiceDocument } from './workspace.model';

function service(name: string): ServiceDocument {
  return {
    name,
    image: `${name}:1`,
    ports: [],
    networks: [],
    volumes: [],
    dependsOn: [],
    environment: {},
  };
}

function topologyOf(...names: string[]) {
  return { services: names.map(service), networks: [], volumes: [] };
}

function simulationResult(startOrder: string[]) {
  return {
    succeeded: true,
    completed: true,
    startOrder,
    events: [],
    issues: [],
    reachability: [],
    inferredConnections: [],
  };
}

function summary(id: string, name: string) {
  return {
    id,
    name,
    topologySchemaVersion: 1,
    createdAt: '2026-01-01T00:00:00+00:00',
    updatedAt: '2026-01-01T00:00:00+00:00',
  };
}

function projectResponse(id: string, name: string, services: string[]) {
  return {
    id,
    name,
    topology: topologyOf(...services),
    topologySchemaVersion: 1,
    createdAt: '2026-01-01T00:00:00+00:00',
    updatedAt: '2026-01-01T00:00:00+00:00',
  };
}

describe('WorkspaceStore', () => {
  let store: WorkspaceStore;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), WorkspaceStore],
    });

    store = TestBed.inject(WorkspaceStore);
    backend = TestBed.inject(HttpTestingController);
  });

  describe('applying YAML', () => {
    it('leaves the working topology untouched when the file cannot be applied', () => {
      store.addService(service('api'));

      store.beginYamlDraft();
      store.updateYamlDraft('services:\n  api:\n    restart: always\n');
      store.applyYamlDraft();

      const blocked: ParseComposeResponse = {
        canApply: false,
        topology: null,
        findings: [
          {
            code: 'compose.key_not_modeled',
            path: 'services.api.restart',
            line: 3,
            column: 5,
            message: 'restart is valid Compose, but ComposeLab does not model it yet.',
            suggestion: 'Remove it to work on this architecture in ComposeLab.',
          },
        ],
      };

      backend.expectOne('/api/compose/parse').flush(blocked);

      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api']);
      expect(store.parseFindings()).toHaveLength(1);

      // The draft stays open, so the learner can fix the file rather than losing their edits.
      expect(store.isEditingYaml()).toBe(true);
    });

    it('replaces the whole topology when the file is fully understood', () => {
      store.addService(service('api'));

      store.beginYamlDraft();
      store.updateYamlDraft('services:\n  cache:\n    image: redis:8\n');
      store.applyYamlDraft();

      backend.expectOne('/api/compose/parse').flush({
        canApply: true,
        topology: { services: [service('cache')], networks: [], volumes: [] },
        findings: [],
      } satisfies ParseComposeResponse);

      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['cache']);
      expect(store.isEditingYaml()).toBe(false);
      expect(store.parseFindings()).toHaveLength(0);
    });

    /**
     * The race: Apply is clicked, the learner keeps typing, and the answer about the older YAML comes
     * back. It must not replace the topology, and it must not close the newer draft.
     */
    it('ignores a parse response for a draft the learner has moved past', () => {
      store.addService(service('api'));

      // 1. Draft A
      store.beginYamlDraft();
      store.updateYamlDraft('services:\n  cache:\n    image: redis:8\n');

      // 2. Apply A, leaving the request in flight
      store.applyYamlDraft();
      const inFlight = backend.expectOne('/api/compose/parse');

      // 3. Edit to Draft B
      store.updateYamlDraft('services:\n  queue:\n    image: rabbitmq:4\n');

      // 4. A successful parse for A returns
      inFlight.flush({
        canApply: true,
        topology: { services: [service('cache')], networks: [], volumes: [] },
        findings: [
          {
            code: 'compose.key_not_modeled',
            path: 'services.cache.restart',
            line: 4,
            column: 5,
            message: 'stale finding',
            suggestion: 'stale suggestion',
          },
        ],
      } satisfies ParseComposeResponse);

      // 5. The topology is unchanged.
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api']);

      // 6. Draft B is still open, and still says what the learner typed.
      expect(store.isEditingYaml()).toBe(true);
      expect(store.yamlDraft()).toBe('services:\n  queue:\n    image: rabbitmq:4\n');

      // 7. Nothing from the stale response is shown.
      expect(store.parseFindings()).toHaveLength(0);
    });

    it('applies the newer draft when it is submitted in turn', () => {
      store.beginYamlDraft();
      store.updateYamlDraft('draft a');
      store.applyYamlDraft();

      const stale = backend.expectOne('/api/compose/parse');

      store.updateYamlDraft('draft b');
      store.applyYamlDraft();

      const current = backend.expectOne('/api/compose/parse');

      // The obsolete answer lands first and is ignored; the current one is honoured.
      stale.flush({ canApply: true, topology: topologyOf('cache'), findings: [] });
      current.flush({ canApply: true, topology: topologyOf('queue'), findings: [] });

      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['queue']);
      expect(store.isEditingYaml()).toBe(false);
    });
  });

  describe('correspondence', () => {
    it('is suspended while a draft diverges from the generated document', () => {
      store.beginYamlDraft();

      expect(store.correspondenceEnabled()).toBe(true);

      store.updateYamlDraft('services:\n  api:\n    image: api:1\n');

      expect(store.hasUnappliedYamlEdits()).toBe(true);
      expect(store.correspondenceEnabled()).toBe(false);

      // Nothing is highlighted while the ranges no longer describe what is on screen.
      store.select({ kind: 'service', name: 'api', ownerService: null, detail: null });
      expect(store.highlightedLines()).toEqual([]);
    });

    it('resumes once the draft is discarded', () => {
      store.beginYamlDraft();
      store.updateYamlDraft('changed');
      store.discardYamlDraft();

      expect(store.correspondenceEnabled()).toBe(true);
      expect(store.isEditingYaml()).toBe(false);
    });
  });

  describe('simulation', () => {
    it('never runs on its own', () => {
      store.addService(service('api'));
      store.updateService('api', { image: 'api:2' });

      // Editing must not produce a simulation. It is an explicit learner action.
      backend.expectNone('/api/topology/simulate');
      expect(store.simulation()).toBeNull();
    });

    it('drops a stale result when the architecture changes underneath it', () => {
      store.simulate();

      backend.expectOne('/api/topology/simulate').flush(simulationResult(['api']));

      expect(store.simulation()).not.toBeNull();

      store.addService(service('cache'));

      // A timeline describes the architecture it was run against.
      expect(store.simulation()).toBeNull();
    });

    /**
     * The inverse of the case above, and the one that actually races: the request is already in
     * flight when the topology moves on, so the response arrives describing an architecture that no
     * longer exists. Keyed on a revision rather than on timing.
     */
    it('ignores a response that arrives after the topology has moved on', () => {
      store.addService(service('api'));

      store.simulate();

      const inFlight = backend.expectOne('/api/topology/simulate');

      // The learner keeps working while the request is outstanding.
      store.addService(service('cache'));

      inFlight.flush(simulationResult(['api']));

      expect(store.simulation()).toBeNull();
      expect(store.isSimulating()).toBe(false);
    });

    it('keeps a response that arrives while the topology is unchanged', () => {
      store.addService(service('api'));

      store.simulate();

      const inFlight = backend.expectOne('/api/topology/simulate');

      // Selecting something is not a change to the architecture.
      store.select({ kind: 'service', name: 'api', ownerService: null, detail: null });

      inFlight.flush(simulationResult(['api']));

      expect(store.simulation()?.startOrder).toEqual(['api']);
    });
  });

  describe('saving', () => {
    it('creates on first save and updates afterwards, only when asked', () => {
      store.addService(service('api'));

      expect(store.hasUnsavedChanges()).toBe(true);

      store.save();

      const created = backend.expectOne('/api/projects');
      expect(created.request.method).toBe('POST');
      created.flush('11111111-1111-1111-1111-111111111111');

      expect(store.isSaved()).toBe(true);
      expect(store.hasUnsavedChanges()).toBe(false);

      store.addService(service('cache'));
      expect(store.hasUnsavedChanges()).toBe(true);

      store.save();

      const updated = backend.expectOne('/api/projects/11111111-1111-1111-1111-111111111111');
      expect(updated.request.method).toBe('PUT');
    });

    it('saves an architecture the engine would call broken', () => {
      // Duplicate names and a colliding host port: saving is not a quality gate.
      store.addService({ ...service('api'), ports: [{ hostPort: 8080, containerPort: 8080 }] });
      store.addService({ ...service('api'), ports: [{ hostPort: 8080, containerPort: 9090 }] });

      store.save();

      expect(backend.expectOne('/api/projects').request.method).toBe('POST');
    });

    /** Renaming a saved project is a change to the project, so it must read as unsaved. */
    it('counts a rename as an unsaved change', () => {
      store.addService(service('api'));
      store.save();
      backend.expectOne('/api/projects').flush('11111111-1111-1111-1111-111111111111');
      backend.expectOne('/api/projects').flush({ projects: [] });

      expect(store.hasUnsavedChanges()).toBe(false);

      store.rename('E-commerce lab');

      expect(store.hasUnsavedChanges()).toBe(true);

      store.save();
      backend.expectOne('/api/projects/11111111-1111-1111-1111-111111111111').flush(null);
      backend.expectOne('/api/projects').flush({ projects: [] });

      expect(store.hasUnsavedChanges()).toBe(false);
    });

    /** Summaries refresh because a save succeeded, not because a timer elapsed. */
    it('refreshes the project list as a consequence of a successful save', () => {
      store.addService(service('api'));
      store.save();

      backend.expectOne('/api/projects').flush('22222222-2222-2222-2222-222222222222');

      // The list request is issued by the save completing.
      backend
        .expectOne('/api/projects')
        .flush({ projects: [summary('22222222-2222-2222-2222-222222222222', 'Saved')] });

      expect(store.savedProjects().map((project) => project.name)).toEqual(['Saved']);
    });

    it('does not refresh the list when a save fails', () => {
      store.addService(service('api'));
      store.save();

      backend
        .expectOne('/api/projects')
        .flush({ title: 'nope' }, { status: 500, statusText: 'Server Error' });

      backend.expectNone('/api/projects');
      expect(store.hasUnsavedChanges()).toBe(true);
    });
  });

  describe('opening another project', () => {
    /**
     * ComposeLab saves only when told to, so switching projects with unsaved work has to be a
     * decision rather than something that happens quietly.
     */
    it('waits for a decision when there are unsaved changes', () => {
      store.refreshProjects();
      backend.expectOne('/api/projects').flush({ projects: [summary('abc', 'Other lab')] });

      store.addService(service('api'));

      store.requestOpenProject('abc');

      // Nothing has been fetched and nothing has been replaced.
      backend.expectNone('/api/projects/abc');
      expect(store.pendingProjectId()).toBe('abc');
      expect(store.pendingProjectName()).toBe('Other lab');
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api']);
    });

    it('keeps the workspace when the decision is to stay', () => {
      store.addService(service('api'));
      store.requestOpenProject('abc');

      store.cancelPendingOpen();

      expect(store.pendingProjectId()).toBeNull();
      backend.expectNone('/api/projects/abc');
      expect(store.authoredTopology().services).toHaveLength(1);
    });

    it('replaces the workspace only once the change is explicitly discarded', () => {
      store.addService(service('api'));
      store.requestOpenProject('abc');

      store.discardChangesAndOpenPending();

      backend.expectOne('/api/projects/abc').flush({
        id: 'abc',
        name: 'Other lab',
        topology: topologyOf('cache'),
        topologySchemaVersion: 1,
        createdAt: '2026-01-01T00:00:00+00:00',
        updatedAt: '2026-01-01T00:00:00+00:00',
      });

      expect(store.pendingProjectId()).toBeNull();
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['cache']);
      expect(store.currentProjectName()).toBe('Other lab');

      // A freshly loaded project is not dirty.
      expect(store.hasUnsavedChanges()).toBe(false);
    });

    it('opens straight away when there is nothing to lose', () => {
      store.requestOpenProject('abc');

      expect(store.pendingProjectId()).toBeNull();
      expect(backend.expectOne('/api/projects/abc').request.method).toBe('GET');
    });

    /**
     * Two loads can overlap, and the first can answer last. Only the most recent request may change
     * the workspace — enforced in the store, not by hoping the selector is disabled in time.
     */
    it('lets only the most recent load change the workspace', () => {
      // 1. Request project A.
      store.requestOpenProject('project-a');
      const requestForA = backend.expectOne('/api/projects/project-a');

      // 2. Request project B before A has returned.
      store.requestOpenProject('project-b');
      const requestForB = backend.expectOne('/api/projects/project-b');

      // 3. B returns first and loads.
      requestForB.flush(projectResponse('project-b', 'B lab', ['cache']));

      expect(store.currentProjectId()).toBe('project-b');
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['cache']);

      // 4. A returns afterwards.
      requestForA.flush(projectResponse('project-a', 'A lab', ['api']));

      // 5. B is still what is open, and A's stale response changed nothing.
      expect(store.currentProjectId()).toBe('project-b');
      expect(store.currentProjectName()).toBe('B lab');
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['cache']);
      expect(store.hasUnsavedChanges()).toBe(false);
    });

    it('keeps the workspace clean when a discarded switch is overtaken', () => {
      store.addService(service('api'));

      store.requestOpenProject('project-a');
      store.discardChangesAndOpenPending();
      const requestForA = backend.expectOne('/api/projects/project-a');

      // The workspace is still dirty while A is in flight, so switching again is another decision.
      store.requestOpenProject('project-b');
      expect(store.pendingProjectId()).toBe('project-b');

      store.discardChangesAndOpenPending();
      const requestForB = backend.expectOne('/api/projects/project-b');

      requestForB.flush(projectResponse('project-b', 'B lab', ['queue']));
      requestForA.flush(projectResponse('project-a', 'A lab', ['api']));

      expect(store.currentProjectName()).toBe('B lab');
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['queue']);

      // History was reset by B's load, and A did not resurrect anything.
      expect(store.canUndo()).toBe(false);
    });
  });

  describe('history', () => {
    it('steps back and forward through topology changes', () => {
      store.addService(service('api'));
      store.addService(service('cache'));

      expect(store.canUndo()).toBe(true);

      store.undo();
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api']);

      store.undo();
      expect(store.authoredTopology().services).toHaveLength(0);
      expect(store.canUndo()).toBe(false);

      store.redo();
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api']);

      store.redo();
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api', 'cache']);
      expect(store.canRedo()).toBe(false);
    });

    it('makes a new change discard the redone future', () => {
      store.addService(service('api'));
      store.undo();

      expect(store.canRedo()).toBe(true);

      store.addService(service('queue'));

      expect(store.canRedo()).toBe(false);
      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['queue']);
    });

    /** Undo goes through the same replacement path, so it invalidates a simulation like any edit. */
    it('invalidates a simulation, including one still in flight', () => {
      store.addService(service('api'));

      store.simulate();
      backend.expectOne('/api/topology/simulate').flush(simulationResult(['api']));
      expect(store.simulation()).not.toBeNull();

      store.undo();
      expect(store.simulation()).toBeNull();

      store.simulate();
      const inFlight = backend.expectOne('/api/topology/simulate');

      store.redo();
      inFlight.flush(simulationResult(['api']));

      expect(store.simulation()).toBeNull();
    });

    it('records a successful Apply YAML as one entry', () => {
      store.addService(service('api'));

      store.beginYamlDraft();
      store.updateYamlDraft('services:\n  cache:\n    image: redis:8\n');
      store.applyYamlDraft();

      backend.expectOne('/api/compose/parse').flush({
        canApply: true,
        topology: topologyOf('cache'),
        findings: [],
      });

      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['cache']);

      store.undo();

      expect(store.authoredTopology().services.map((item) => item.name)).toEqual(['api']);
    });

    it('records nothing for a blocked Apply YAML', () => {
      store.addService(service('api'));

      const before = store.canUndo();

      store.beginYamlDraft();
      store.updateYamlDraft('services:\n  api:\n    restart: always\n');
      store.applyYamlDraft();

      backend.expectOne('/api/compose/parse').flush({
        canApply: false,
        topology: null,
        findings: [
          {
            code: 'compose.key_not_modeled',
            path: 'services.api.restart',
            line: 3,
            column: 5,
            message: 'not modeled',
            suggestion: 'remove it',
          },
        ],
      });

      store.discardYamlDraft();

      // Nothing changed, so there is nothing extra to undo.
      expect(store.canUndo()).toBe(before);
      store.undo();
      expect(store.authoredTopology().services).toHaveLength(0);
    });

    /** Undo moves the topology, which regenerates the YAML underneath an edit in progress. */
    it('is unavailable while a draft diverges from the generated document', () => {
      store.addService(service('api'));

      store.beginYamlDraft();
      store.updateYamlDraft('half-typed');

      expect(store.canUndo()).toBe(false);
      expect(store.canRedo()).toBe(false);

      store.undo();
      expect(store.authoredTopology().services).toHaveLength(1);

      store.discardYamlDraft();
      expect(store.canUndo()).toBe(true);
    });

    it('never lets undo cross a project boundary', () => {
      store.addService(service('api'));
      store.addService(service('cache'));

      store.requestOpenProject('abc');
      store.discardChangesAndOpenPending();

      backend.expectOne('/api/projects/abc').flush({
        id: 'abc',
        name: 'Other lab',
        topology: topologyOf('queue'),
        topologySchemaVersion: 1,
        createdAt: '2026-01-01T00:00:00+00:00',
        updatedAt: '2026-01-01T00:00:00+00:00',
      });

      expect(store.canUndo()).toBe(false);
      expect(store.canRedo()).toBe(false);
    });

    it('keeps history across a save', () => {
      store.addService(service('api'));
      store.save();

      backend.expectOne('/api/projects').flush('33333333-3333-3333-3333-333333333333');
      backend.expectOne('/api/projects').flush({ projects: [] });

      expect(store.canUndo()).toBe(true);

      store.undo();

      expect(store.authoredTopology().services).toHaveLength(0);
      // Undoing past a save is a real change again.
      expect(store.hasUnsavedChanges()).toBe(true);
    });

    it('caps the history rather than growing without bound', () => {
      for (let index = 0; index < 60; index += 1) {
        store.addService(service(`service-${index}`));
      }

      let undone = 0;

      while (store.canUndo() && undone < 100) {
        store.undo();
        undone += 1;
      }

      expect(undone).toBe(50);

      // The oldest snapshots were dropped, so the earliest reachable state is not the empty one.
      expect(store.authoredTopology().services.length).toBeGreaterThan(0);
    });
  });
});
