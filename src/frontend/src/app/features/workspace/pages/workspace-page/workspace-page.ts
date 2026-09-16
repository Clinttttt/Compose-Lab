import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Icon } from '@shared/icon/icon';
import { WorkspaceApi } from '../../workspace-api';
import { WorkspaceStore } from '../../workspace-store';
import { ProjectSummary } from '../../workspace.model';
import { ComponentPalette } from '../../ui/component-palette/component-palette';
import { ElementInspector } from '../../ui/element-inspector/element-inspector';
import { SimulationConsole } from '../../ui/simulation-console/simulation-console';
import { TopologyDiagram } from '../../ui/topology-diagram/topology-diagram';
import { YamlPane } from '../../ui/yaml-pane/yaml-pane';
import { signal } from '@angular/core';

/**
 * The workspace screen: a toolbar and five panels around one store.
 *
 * The panels are presentation boundaries, not independent owners of state. None of them holds its own
 * copy of the topology or its own opinion about whether the architecture is sound.
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
  private readonly api = inject(WorkspaceApi);

  protected readonly projectName = this.store.currentProjectName;
  protected readonly isSaved = this.store.isSaved;
  protected readonly isSaving = this.store.isSaving;
  protected readonly hasUnsavedChanges = this.store.hasUnsavedChanges;
  protected readonly validation = this.store.validation;

  protected readonly savedProjects = signal<readonly ProjectSummary[]>([]);

  ngOnInit(): void {
    this.refreshProjects();
  }

  protected rename(event: Event): void {
    this.store.rename((event.target as HTMLInputElement).value);
  }

  /** The only thing that writes to storage. */
  protected save(): void {
    this.store.save();
    // The list reflects the new or updated project once the save has been accepted.
    setTimeout(() => this.refreshProjects(), 300);
  }

  protected open(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;

    if (id !== '') {
      this.store.loadProject(id);
    }
  }

  private refreshProjects(): void {
    this.api.listProjects().subscribe({
      next: (result) => this.savedProjects.set(result.projects),
      error: () => {
        // Reported by errorInterceptor.
      },
    });
  }
}
