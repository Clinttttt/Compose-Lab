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
  });
});
