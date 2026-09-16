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

      backend.expectOne('/api/topology/simulate').flush({
        succeeded: true,
        completed: true,
        startOrder: ['api'],
        events: [],
        issues: [],
        reachability: [],
        inferredConnections: [],
      });

      expect(store.simulation()).not.toBeNull();

      store.addService(service('cache'));

      // A timeline describes the architecture it was run against.
      expect(store.simulation()).toBeNull();
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
  });
});
