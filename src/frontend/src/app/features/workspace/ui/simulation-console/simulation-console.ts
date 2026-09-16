import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon, IconName } from '@shared/icon/icon';
import { ConsoleDock, PanelLayoutStore } from '@core/preferences/panel-layout-store';
import { WorkspaceStore } from '../../workspace-store';
import { ArchitectureIssue, ElementReference, IssueSeverity } from '../../workspace.model';

/**
 * The simulation console.
 *
 * Every event, issue, severity, and explanation shown here came from the backend. Nothing is
 * manufactured client-side: this component decides layout and ordering only, and never decides
 * whether an architecture is correct.
 *
 * It behaves like a tool window: it can sit along the bottom, along the right, or be collapsed to its
 * header. Collapsed keeps the run button and the tallies reachable, because hiding the control that
 * produces the output would be a strange way to save space.
 */
@Component({
  selector: 'app-simulation-console',
  imports: [Icon],
  templateUrl: './simulation-console.html',
  styleUrl: './simulation-console.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-dock]': 'dock()' },
})
export class SimulationConsole {
  private readonly store = inject(WorkspaceStore);
  private readonly panels = inject(PanelLayoutStore);

  protected readonly dock = this.panels.consoleDock;
  protected readonly isCollapsed = computed(() => this.dock() === 'collapsed');

  protected readonly simulation = this.store.simulation;
  protected readonly isSimulating = this.store.isSimulating;
  protected readonly validation = this.store.validation;
  protected readonly hasContent = this.store.hasContent;

  protected readonly docks: readonly { value: ConsoleDock; icon: IconName; label: string }[] = [
    { value: 'bottom', icon: 'dock-bottom', label: 'Dock along the bottom' },
    { value: 'right', icon: 'dock-right', label: 'Dock along the right' },
    { value: 'collapsed', icon: 'collapse', label: 'Collapse to the header' },
  ];

  /**
   * Before a run, the cheap check is what there is to show. Afterwards, the run's own findings.
   *
   * Sorted worst-first: when an architecture breaks, the reason has to be the first thing on screen,
   * not below a note about something that is merely worth knowing.
   */
  protected readonly issues = computed<readonly ArchitectureIssue[]>(() => {
    const found = this.simulation()?.issues ?? this.validation()?.issues ?? [];

    return [...found].sort((left, right) => weight(right.severity) - weight(left.severity));
  });

  protected readonly isComplete = computed(
    () => this.simulation()?.completed ?? this.validation()?.isComplete ?? true,
  );

  protected readonly errorCount = computed(
    () => this.issues().filter((issue) => issue.severity === 'error').length,
  );

  protected readonly warningCount = computed(
    () => this.issues().filter((issue) => issue.severity === 'warning').length,
  );

  protected run(): void {
    this.store.simulate();
  }

  protected iconFor(severity: IssueSeverity): IconName {
    return severity === 'error' ? 'error' : severity === 'warning' ? 'warning' : 'information';
  }

  protected labelFor(severity: IssueSeverity): string {
    return severity === 'error' ? 'Problem' : severity === 'warning' ? 'Review' : 'Note';
  }

  /** Selecting an issue's element is what connects an explanation back to the architecture. */
  protected focus(element: ElementReference): void {
    this.store.select(element);
  }

  protected describe(element: ElementReference): string {
    return element.ownerService === null
      ? element.name
      : `${element.ownerService} → ${element.name}`;
  }

  protected count(value: number, singular: string, plural: string): string {
    return `${value} ${value === 1 ? singular : plural}`;
  }

  protected setDock(dock: ConsoleDock): void {
    this.panels.setConsoleDock(dock);
  }
}

function weight(severity: IssueSeverity): number {
  return severity === 'error' ? 2 : severity === 'warning' ? 1 : 0;
}
