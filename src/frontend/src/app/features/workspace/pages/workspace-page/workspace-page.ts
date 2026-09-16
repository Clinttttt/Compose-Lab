import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Icon } from '@shared/icon/icon';
import { WorkspaceStore } from '../../workspace-store';
import { ComponentPalette } from '../../ui/component-palette/component-palette';
import { ElementInspector } from '../../ui/element-inspector/element-inspector';
import { SimulationConsole } from '../../ui/simulation-console/simulation-console';
import { TopologyDiagram } from '../../ui/topology-diagram/topology-diagram';
import { YamlPane } from '../../ui/yaml-pane/yaml-pane';

/**
 * The workspace screen: a toolbar and five panels around one store.
 *
 * The panels are presentation boundaries, not independent owners of state. None of them holds its own
 * copy of the topology or its own opinion about whether the architecture is sound, and the project
 * list is the store's too — it refreshes because a save succeeded, never on a timer.
 */
@Component({
  selector: 'app-workspace-page',
  imports: [Icon, ComponentPalette, TopologyDiagram, ElementInspector, YamlPane, SimulationConsole],
  templateUrl: './workspace-page.html',
  styleUrl: './workspace-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspacePage implements OnInit {
  private readonly store = inject(WorkspaceStore);

  protected readonly projectName = this.store.currentProjectName;
  protected readonly isSaved = this.store.isSaved;
  protected readonly isSaving = this.store.isSaving;
  protected readonly hasUnsavedChanges = this.store.hasUnsavedChanges;
  protected readonly validation = this.store.validation;
  protected readonly savedProjects = this.store.savedProjects;
  protected readonly pendingProjectName = this.store.pendingProjectName;

  ngOnInit(): void {
    this.store.refreshProjects();
  }

  protected rename(event: Event): void {
    this.store.rename((event.target as HTMLInputElement).value);
  }

  /** The only thing that writes to storage. */
  protected save(): void {
    this.store.save();
  }

  /** Asks to open. With unsaved work the store waits for a decision rather than replacing it. */
  protected requestOpen(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const id = select.value;

    if (id !== '') {
      this.store.requestOpenProject(id);
    }

    // The pending decision is what carries the intent from here on.
    select.value = '';
  }

  protected discardAndOpen(): void {
    this.store.discardChangesAndOpenPending();
  }

  protected cancelOpen(): void {
    this.store.cancelPendingOpen();
  }
}
