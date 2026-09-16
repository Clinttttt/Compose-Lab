import { computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, finalize, of, switchMap } from 'rxjs';
import { WorkspaceApi } from './workspace-api';
import {
  ComposeFinding,
  ElementReference,
  emptyTopology,
  GenerateComposeResponse,
  ProjectSummary,
  ProvenanceEntry,
  sameElement,
  ServiceDocument,
  SimulateResponse,
  TopologyDocument,
  ValidateResponse,
} from './workspace.model';

/** The name a workspace starts with, so an untouched one does not read as having unsaved work. */
const INITIAL_NAME = 'Untitled architecture';

/**
 * How many topology snapshots to keep.
 *
 * A cap rather than command objects or diffs: a topology is a small document, and storing whole
 * snapshots keeps undo a plain assignment through the normal replacement path instead of a second way
 * to mutate state that could drift from the first.
 */
const HISTORY_LIMIT = 50;

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

  /**
   * Incremented on every replacement. An in-flight request that returns after the architecture has
   * moved on carries an answer to a question nobody is asking any more, and comparing revisions is
   * how that is detected without depending on timing.
   */
  private readonly revision = signal(0);

  readonly authoredTopology = this.topology.asReadonly();

  // --- History --------------------------------------------------------------

  /**
   * Authored topology snapshots, and nothing else.
   *
   * Selection, the YAML draft, generated YAML, validation, simulation, and project metadata are not
   * history: undoing should take back a change to the architecture, not restore which node happened to
   * be selected at the time.
   */
  private readonly undoStack = signal<readonly TopologyDocument[]>([]);

  private readonly redoStack = signal<readonly TopologyDocument[]>([]);

  /**
   * Disabled while a draft diverges from the generated document. Undo moves the topology, which
   * regenerates the YAML underneath an edit in progress — so it waits until the draft is applied or
   * discarded.
   */
  readonly canUndo = computed(() => this.undoStack().length > 0 && !this.hasUnappliedYamlEdits());

  readonly canRedo = computed(() => this.redoStack().length > 0 && !this.hasUnappliedYamlEdits());

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

  /** True when the selected network is one the author declared, rather than one Compose supplies. */
  readonly selectedNetworkIsDeclared = computed(() => {
    const selected = this.selection();

    return (
      selected !== null &&
      selected.kind === 'network' &&
      this.topology().networks.some((network) => network.name === selected.name)
    );
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

  /**
   * Incremented on every change to the draft, including opening and discarding one.
   *
   * Apply captures this and refuses a response that comes back against a draft the learner has since
   * changed. A revision rather than a text comparison, because it also catches discarding a draft and
   * reopening one that happens to read the same: that is a different draft, and an answer about the
   * first one has no business closing it.
   */
  private readonly draftRevision = signal(0);

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

  private readonly projectName = signal(INITIAL_NAME);

  /** Name and topology as last saved. Null when this has never been saved. */
  private readonly savedSnapshot = signal<string | null>(null);

  private readonly saving = signal(false);

  private readonly projects = signal<readonly ProjectSummary[]>([]);

  /** Set when a project switch is waiting on a decision about unsaved work. */
  private readonly pendingOpen = signal<string | null>(null);

  /**
   * Incremented for every project load. Two loads can overlap — pick one project, change your mind,
   * pick another — and the first may answer last. Only the most recent request may change the
   * workspace, or a slow response would quietly replace the project the learner actually opened.
   */
  private readonly projectLoadRevision = signal(0);

  readonly currentProjectId = this.projectId.asReadonly();

  readonly currentProjectName = this.projectName.asReadonly();

  readonly isSaving = this.saving.asReadonly();

  readonly isSaved = computed(() => this.projectId() !== null);

  readonly savedProjects = this.projects.asReadonly();

  readonly pendingProjectId = this.pendingOpen.asReadonly();

  readonly pendingProjectName = computed(() => {
    const id = this.pendingOpen();

    return this.projects().find((project) => project.id === id)?.name ?? null;
  });

  /**
   * Unsaved changes covers the name as well as the architecture: renaming a saved project is a change
   * to the project, and an explicit save is the only thing that clears it.
   */
  readonly hasUnsavedChanges = computed(() => {
    const snapshot = this.savedSnapshot();

    return snapshot === null
      ? this.hasContent() || this.projectName() !== INITIAL_NAME
      : this.snapshotOf(this.projectName(), this.topology()) !== snapshot;
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
    this.draftRevision.update((current) => current + 1);
  }

  updateYamlDraft(yaml: string): void {
    this.draft.set(yaml);
    this.draftRevision.update((current) => current + 1);
  }

  discardYamlDraft(): void {
    this.draft.set(null);
    this.composeFindings.set([]);
    this.draftRevision.update((current) => current + 1);
  }

  /**
   * All or nothing. Any key the backend reports as unmodeled or unknown blocks the apply, and the
   * working topology is left exactly as it was — a partial apply would leave the learner with an
   * architecture that quietly disagrees with the file they wrote.
   *
   * The answer is also checked against the draft it was asked about. Typing while the request is in
   * flight would otherwise let a result for the older YAML replace the topology and close the newer
   * draft.
   */
  applyYamlDraft(): void {
    const draft = this.draft();

    if (draft === null) {
      return;
    }

    const submittedFor = this.draftRevision();

    this.api.parse(draft).subscribe({
      next: (result) => {
        if (this.draftRevision() !== submittedFor) {
          // An answer about YAML the learner has already moved past. Neither its findings nor its
          // topology may touch current state.
          return;
        }

        this.composeFindings.set(result.findings);

        if (result.canApply && result.topology !== null) {
          this.replace(result.topology);
          this.draft.set(null);
          this.draftRevision.update((current) => current + 1);
          this.selection.set(null);
        }
      },
      error: () => {
        // Reported by errorInterceptor. The topology stays untouched either way.
      },
    });
  }

  // --- History --------------------------------------------------------------

  /** Steps back one topology change. Goes through the same replacement path as any other edit. */
  undo(): void {
    if (!this.canUndo()) {
      return;
    }

    const stack = this.undoStack();
    const previous = stack[stack.length - 1];

    this.undoStack.set(stack.slice(0, -1));
    this.redoStack.update((stack) => trim([...stack, this.topology()]));

    this.replace(previous, false);
  }

  redo(): void {
    if (!this.canRedo()) {
      return;
    }

    const stack = this.redoStack();
    const next = stack[stack.length - 1];

    this.redoStack.set(stack.slice(0, -1));
    this.undoStack.update((stack) => trim([...stack, this.topology()]));

    this.replace(next, false);
  }

  // --- Simulation -----------------------------------------------------------

  /** Explicit. Nothing in this store runs a simulation on its own. */
  simulate(): void {
    const requestedFor = this.revision();

    this.simulationRunning.set(true);

    this.api
      .simulate(this.topology())
      .pipe(finalize(() => this.simulationRunning.set(false)))
      .subscribe({
        next: (result) => {
          // Discard an answer about an architecture that has since changed.
          if (this.revision() === requestedFor) {
            this.simulationResult.set(result);
          }
        },
        error: () => {
          // Reported by errorInterceptor.
        },
      });
  }

  // --- Projects -------------------------------------------------------------

  refreshProjects(): void {
    this.api.listProjects().subscribe({
      next: (result) => this.projects.set(result.projects),
      error: () => {
        // Reported by errorInterceptor.
      },
    });
  }

  /**
   * Asks to open a project. With unsaved work this waits for a decision instead of replacing it:
   * ComposeLab saves only when told to, so a silent switch would discard the learner's work.
   */
  requestOpenProject(id: string): void {
    if (this.hasUnsavedChanges()) {
      this.pendingOpen.set(id);

      return;
    }

    this.loadProject(id);
  }

  discardChangesAndOpenPending(): void {
    const id = this.pendingOpen();

    this.pendingOpen.set(null);

    if (id !== null) {
      this.loadProject(id);
    }
  }

  cancelPendingOpen(): void {
    this.pendingOpen.set(null);
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
            this.markSaved(request.name, request.topology);
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
        next: () => this.markSaved(request.name, request.topology),
        error: () => {
          // Reported by errorInterceptor.
        },
      });
  }

  private loadProject(id: string): void {
    const requestedFor = this.projectLoadRevision() + 1;

    this.projectLoadRevision.set(requestedFor);

    this.api.getProject(id).subscribe({
      next: (project) => {
        if (this.projectLoadRevision() !== requestedFor) {
          // An older load answering late. The learner has already opened something else.
          return;
        }

        this.projectId.set(project.id);
        this.projectName.set(project.name);
        this.replace(project.topology);
        this.savedSnapshot.set(this.snapshotOf(project.name, project.topology));
        this.draft.set(null);
        this.draftRevision.update((current) => current + 1);
        this.selection.set(null);

        // History cannot cross a project boundary: undoing into another architecture would be
        // meaningless, and saving afterwards would write it.
        this.undoStack.set([]);
        this.redoStack.set([]);
      },
      error: () => {
        // Reported by errorInterceptor. A 409 means the saved schema is one this build cannot read.
      },
    });
  }

  /** Summaries refresh because a save succeeded, not because time passed. */
  private markSaved(name: string, topology: TopologyDocument): void {
    this.savedSnapshot.set(this.snapshotOf(name, topology));
    this.refreshProjects();
  }

  private snapshotOf(name: string, topology: TopologyDocument): string {
    return JSON.stringify({ name, topology });
  }

  /**
   * Replaces the working topology. Any simulation result is dropped and the revision moves on: a
   * timeline describes the architecture it was run against, and an in-flight request for the previous
   * one must not be allowed to land.
   *
   * Undo and redo come through here too, with <paramref name="recordHistory"/> false — one path that
   * changes the topology, so nothing can move it without advancing the revision.
   */
  private replace(next: TopologyDocument, recordHistory = true): void {
    if (recordHistory) {
      this.undoStack.update((stack) => trim([...stack, this.topology()]));

      // A new change makes the redone future unreachable.
      this.redoStack.set([]);
    }

    this.topology.set(next);
    this.revision.update((current) => current + 1);
    this.simulationResult.set(null);
  }
}

function trim(snapshots: readonly TopologyDocument[]): readonly TopologyDocument[] {
  return snapshots.length > HISTORY_LIMIT
    ? snapshots.slice(snapshots.length - HISTORY_LIMIT)
    : snapshots;
}
