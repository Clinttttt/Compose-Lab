import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon, IconName } from '@shared/icon/icon';
import { WorkspaceStore } from '../../workspace-store';
import { ArchitectureIssue, ElementReference, IssueSeverity } from '../../workspace.model';

/**
 * The simulation console.
 *
 * Every event, issue, severity, and explanation shown here came from the backend. Nothing is
 * manufactured client-side: this component decides layout and ordering only, and never decides
 * whether an architecture is correct.
 */
@Component({
  selector: 'app-simulation-console',
  imports: [Icon],
  templateUrl: './simulation-console.html',
  styleUrl: './simulation-console.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SimulationConsole {
  private readonly store = inject(WorkspaceStore);

  protected readonly simulation = this.store.simulation;
  protected readonly isSimulating = this.store.isSimulating;
  protected readonly validation = this.store.validation;
  protected readonly hasContent = this.store.hasContent;

  /** Before a run, the cheap check is what there is to show. Afterwards, the run's own findings. */
  protected readonly issues = computed<readonly ArchitectureIssue[]>(
    () => this.simulation()?.issues ?? this.validation()?.issues ?? [],
  );

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
}
