import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon } from '@shared/icon/icon';
import { WorkspaceStore } from '../../workspace-store';
import {
  ArchitectureIssue,
  ElementReference,
  IssueSeverity,
  ServiceDocument,
} from '../../workspace.model';
import { BAND_HEIGHT, CARD_HEIGHT, CARD_WIDTH, deriveLayout } from './diagram-layout';

interface ServiceCard {
  readonly service: ServiceDocument;
  readonly x: number;
  readonly severity: IssueSeverity | null;
  readonly published: string[];
}

/**
 * The derived diagram.
 *
 * Services, networks, and volumes are real focusable HTML elements, so selection is ordinary focus
 * management and a screen reader reads names rather than shapes. SVG carries only the connector
 * lines, and it is hidden from assistive technology: the same membership is stated on each card and
 * in the inspector, so the lines are never the only place a relationship exists.
 */
@Component({
  selector: 'app-topology-diagram',
  imports: [Icon],
  templateUrl: './topology-diagram.html',
  styleUrl: './topology-diagram.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TopologyDiagram {
  private readonly store = inject(WorkspaceStore);

  protected readonly cardWidth = CARD_WIDTH;
  protected readonly cardHeight = CARD_HEIGHT;
  protected readonly bandHeight = BAND_HEIGHT;

  protected readonly topology = this.store.authoredTopology;
  protected readonly selected = this.store.selectedElement;

  protected readonly layout = computed(() => deriveLayout(this.topology()));

  protected readonly cards = computed<readonly ServiceCard[]>(() => {
    const positions = this.layout().services;

    return this.topology().services.map((service) => ({
      service,
      x: positions.find((node) => node.name === service.name)?.x ?? 0,
      severity: this.severityFor(service.name),
      published: service.ports.map((port) =>
        port.hostPort === null || port.hostPort === undefined
          ? `dynamic → ${port.containerPort}`
          : `${port.hostPort} → ${port.containerPort}`,
      ),
    }));
  });

  protected selectService(service: ServiceDocument): void {
    this.store.select({
      kind: 'service',
      name: service.name,
      ownerService: null,
      detail: null,
    });
  }

  protected selectBand(kind: 'network' | 'volume', name: string): void {
    this.store.select({ kind, name, ownerService: null, detail: null });
  }

  protected isSelected(kind: ElementReference['kind'], name: string): boolean {
    const selected = this.selected();

    return selected !== null && selected.kind === kind && selected.name === name;
  }

  /** The worst severity reported against this service, so the card can carry an icon and a label. */
  private severityFor(serviceName: string): IssueSeverity | null {
    const issues: readonly ArchitectureIssue[] = [
      ...(this.store.simulation()?.issues ?? []),
      ...(this.store.validation()?.issues ?? []),
    ];

    const relevant = issues.filter((issue) =>
      issue.elements.some(
        (element) => element.name === serviceName || element.ownerService === serviceName,
      ),
    );

    if (relevant.some((issue) => issue.severity === 'error')) {
      return 'error';
    }

    if (relevant.some((issue) => issue.severity === 'warning')) {
      return 'warning';
    }

    return null;
  }
}
