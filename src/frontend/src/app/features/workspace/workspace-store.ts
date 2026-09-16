import { computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, finalize, of, switchMap } from 'rxjs';
import { WorkspaceApi } from './workspace-api';
import {
  ComposeFinding,
  ElementReference,
  emptyTopology,
  GenerateComposeResponse,
  ProvenanceEntry,
  sameElement,
  ServiceDocument,
  SimulateResponse,
  TopologyDocument,
  ValidateResponse,
} from './workspace.model';

/**
 * The workspace's single working source of truth.
 *
 * One writable topology. The diagram, inspector, generated YAML, validation state, and the input to
 * a simulation are all derived from it — no panel keeps an editable copy of its own, because two
 * copies is how a diagram and a YAML pane start disagreeing about the same architecture.
 *
 * Feature-scoped rather than root-provided: the workspace route owns it, so it does not outlive the
 * screen it belongs to.
 */
@Injectable()
export class WorkspaceStore {
  private readonly api = inject(WorkspaceApi);

  // --- The working topology -------------------------------------------------

  private readonly topology = signal<TopologyDocument>(emptyTopology());

  readonly authoredTopology = this.topology.asReadonly();

  readonly hasContent = computed(() => {
    const current = this.topology();

    return current.services.length > 0 || current.networks.length > 0 || current.volumes.length > 0;
  });

  // --- Selection ------------------------------------------------------------

  private readonly selection = signal<ElementReference | null>(null);

  readonly selectedElement = this.selection.asReadonly();

  readonly selectedService = computed<ServiceDocument | null>(() => {
    const selected = this.selection();
    const name = selected?.ownerService ?? (selected?.kind === 'service' ? selected.name : null);

    return this.topology().services.find((service) => service.name === name) ?? null;
  });

  // --- Generated YAML, from the backend ------------------------------------

  private readonly generated = signal<GenerateComposeResponse | null>(null);

  readonly generatedYaml = computed(() => this.generated()?.yaml ?? '');

  readonly provenance = computed<readonly ProvenanceEntry[]>(
    () => this.generated()?.provenance ?? [],
  );

  // --- The YAML draft -------------------------------------------------------

  /** Null means the pane is showing generated YAML rather than an edit in progress. */
  private readonly draft = signal<string | null>(null);

  readonly yamlDraft = this.draft.asReadonly();

  readonly isEditingYaml = computed(() => this.draft() !== null);

  readonly hasUnappliedYamlEdits = computed(() => {
    const draft = this.draft();

    return draft !== null && draft !== this.generatedYaml();
  });

  /**
   * Provenance describes the generated document. Once a draft diverges from it the ranges no longer
   * point at what is on screen, so correspondence is suspended rather than shown as if it still held.
   */
  readonly correspondenceEnabled = computed(() => !this.hasUnappliedYamlEdits());

  private readonly composeFindings = signal<readonly ComposeFinding[]>([]);

  readonly parseFindings = this.composeFindings.asReadonly();

  /** The lines the selected element produced. Empty while correspondence is suspended. */
  readonly highlightedLines = computed<readonly number[]>(() => {
    const selected = this.selection();

    if (!this.correspondenceEnabled() || selected === null) {
      return [];
    }

    const entry = this.provenance().find((candidate) => sameElement(candidate.element, selected));

    if (entry === undefined) {
      return [];
    }

    return Array.from(
      { length: entry.endLine - entry.startLine + 1 },
      (_unused, offset) => entry.startLine + offset,
    );
  });

  // --- Validation and simulation -------------------------------------------

  private readonly validationReport = signal<ValidateResponse | null>(null);

  readonly validation = this.validationReport.asReadonly();

  private readonly simulationResult = signal<SimulateResponse | null>(null);

  readonly simulation = this.simulationResult.asReadonly();

  private readonly simulationRunning = signal(false);

  readonly isSimulating = this.simulationRunning.asReadonly();

  // --- The project ----------------------------------------------------------

  private readonly projectId = signal<string | null>(null);

  private readonly projectName = signal('Untitled architecture');

  /** The topology as last saved, for comparison. Null when this has never been saved. */
  private readonly savedSnapshot = signal<string | null>(null);

  private readonly saving = signal(false);

  readonly currentProjectId = this.projectId.asReadonly();

  readonly currentProjectName = this.projectName.asReadonly();

  readonly isSaving = this.saving.asReadonly();

  readonly isSaved = computed(() => this.projectId() !== null);

  /**
   * Unsaved changes. Compares the serialised working topology against the last saved one — adequate
   * because both come from the same contract, and an explicit save is the only thing that clears it.
   */
  readonly hasUnsavedChanges = computed(() => {
    const snapshot = this.savedSnapshot();

    return snapshot === null ? this.hasContent() : JSON.stringify(this.topology()) !== snapshot;
  });

  constructor() {
    const working = toObservable(this.topology);

    // topology → YAML is continuous, and the YAML always comes from the backend.
    working
      .pipe(
        debounceTime(200),
        switchMap((current) => this.api.generate(current).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        if (result !== null) {
          this.generated.set(result);
        }
      });

    // Validation is the cheap check, so it may follow edits. Simulation never does.
    working
      .pipe(
        debounceTime(200),
        switchMap((current) => this.api.validate(current).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        if (result !== null) {
          this.validationReport.set(result);
        }
      });
  }

  // --- Selection ------------------------------------------------------------

  select(element: ElementReference | null): void {
    this.selection.set(element);
  }

  /** Selects whatever produced a line of generated YAML, preferring the most specific element. */
  selectByYamlLine(line: number): void {
    const containing = this.provenance()
      .filter((entry) => line >= entry.startLine && line <= entry.endLine)
      .sort((left, right) => left.endLine - left.startLine - (right.endLine - right.startLine));

    this.selection.set(containing.at(0)?.element ?? null);
  }

  // --- Topology edits -------------------------------------------------------

  addService(service: ServiceDocument): void {
    this.replace({ ...this.topology(), services: [...this.topology().services, service] });
  }

  removeService(name: string): void {
    const current = this.topology();

    this.replace({
      ...current,
      services: current.services.filter((service) => service.name !== name),
    });

    if (this.selection()?.ownerService === name || this.selection()?.name === name) {
      this.selection.set(null);
    }
  }

  /** Applies a patch to one service. The inspector composes the patch; the store owns the write. */
  updateService(name: string, patch: Partial<ServiceDocument>): void {
    const current = this.topology();

    this.replace({
      ...current,
      services: current.services.map((service) =>
        service.name === name ? { ...service, ...patch } : service,
      ),
    });
  }

  declareNetwork(name: string): void {
    const current = this.topology();

    if (current.networks.some((network) => network.name === name)) {
      return;
    }

    this.replace({ ...current, networks: [...current.networks, { name }] });
  }

  removeNetwork(name: string): void {
    const current = this.topology();

    this.replace({
      ...current,
      networks: current.networks.filter((network) => network.name !== name),
      services: current.services.map((service) => ({
        ...service,
        networks: service.networks.filter((attached) => attached !== name),
      })),
    });
  }

  declareVolume(name: string): void {
    const current = this.topology();

    if (current.volumes.some((volume) => volume.name === name)) {
      return;
    }

    this.replace({ ...current, volumes: [...current.volumes, { name }] });
  }

  removeVolume(name: string): void {
    const current = this.topology();

    this.replace({
      ...current,
      volumes: current.volumes.filter((volume) => volume.name !== name),
      services: current.services.map((service) => ({
        ...service,
        volumes: service.volumes.filter((mount) => mount.volume !== name),
      })),
    });
  }

  // --- YAML -----------------------------------------------------------------

  /** Starts editing, seeded with the generated document. */
  beginYamlDraft(): void {
    this.draft.set(this.generatedYaml());
    this.composeFindings.set([]);
  }

  updateYamlDraft(yaml: string): void {
    this.draft.set(yaml);
  }

  discardYamlDraft(): void {
    this.draft.set(null);
    this.composeFindings.set([]);
  }

  /**
   * All or nothing. Any key the backend reports as unmodeled or unknown blocks the apply, and the
   * working topology is left exactly as it was — a partial apply would leave the learner with an
   * architecture that quietly disagrees with the file they wrote.
   */
  applyYamlDraft(): void {
    const draft = this.draft();

    if (draft === null) {
      return;
    }

    this.api.parse(draft).subscribe({
      next: (result) => {
        this.composeFindings.set(result.findings);

        if (result.canApply && result.topology !== null) {
          this.replace(result.topology);
          this.draft.set(null);
          this.selection.set(null);
        }
      },
      error: () => {
        // Reported by errorInterceptor. The topology stays untouched either way.
      },
    });
  }

  // --- Simulation -----------------------------------------------------------

  /** Explicit. Nothing in this store runs a simulation on its own. */
  simulate(): void {
    this.simulationRunning.set(true);

    this.api
      .simulate(this.topology())
      .pipe(finalize(() => this.simulationRunning.set(false)))
      .subscribe({
        next: (result) => this.simulationResult.set(result),
        error: () => {
          // Reported by errorInterceptor.
        },
      });
  }

  // --- Projects -------------------------------------------------------------

  loadProject(id: string): void {
    this.api.getProject(id).subscribe({
      next: (project) => {
        this.projectId.set(project.id);
        this.projectName.set(project.name);
        this.replace(project.topology);
        this.savedSnapshot.set(JSON.stringify(project.topology));
        this.draft.set(null);
        this.selection.set(null);
      },
      error: () => {
        // Reported by errorInterceptor. A 409 means the saved schema is one this build cannot read.
      },
    });
  }

  rename(name: string): void {
    this.projectName.set(name);
  }

  /** The only thing that changes what is stored. There is no autosave. */
  save(): void {
    const request = { name: this.projectName(), topology: this.topology() };
    const id = this.projectId();

    this.saving.set(true);

    if (id === null) {
      this.api
        .createProject(request)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => {
            this.projectId.set(created);
            this.savedSnapshot.set(JSON.stringify(request.topology));
          },
          error: () => {
            // Reported by errorInterceptor.
          },
        });

      return;
    }

    this.api
      .updateProject(id, request)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => this.savedSnapshot.set(JSON.stringify(request.topology)),
        error: () => {
          // Reported by errorInterceptor.
        },
      });
  }

  /**
   * Replaces the working topology. Any simulation result is dropped: a timeline describes the
   * architecture it was run against, and showing it beside a changed one would be a lie.
   */
  private replace(next: TopologyDocument): void {
    this.topology.set(next);
    this.simulationResult.set(null);
  }
}
