import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon } from '@shared/icon/icon';
import { WorkspaceStore } from '../../workspace-store';

interface YamlLine {
  readonly number: number;
  readonly text: string;
  readonly highlighted: boolean;
  /** True when some element produced this line, so selecting it means something. */
  readonly selectable: boolean;
}

/**
 * The YAML pane.
 *
 * Generated YAML is read-only and comes from the backend — no Compose is written in TypeScript. The
 * line ranges a selected element highlights come from the generate response's provenance, never from
 * anything the frontend infers.
 *
 * Editing is asymmetric on purpose: model to YAML is continuous, YAML to model happens only through
 * Apply, and while a draft diverges the correspondence highlighting is suspended rather than shown as
 * though the ranges still matched what is on screen.
 */
@Component({
  selector: 'app-yaml-pane',
  imports: [Icon],
  templateUrl: './yaml-pane.html',
  styleUrl: './yaml-pane.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class YamlPane {
  private readonly store = inject(WorkspaceStore);

  protected readonly isEditing = this.store.isEditingYaml;
  protected readonly draft = this.store.yamlDraft;
  protected readonly findings = this.store.parseFindings;
  protected readonly correspondenceEnabled = this.store.correspondenceEnabled;
  protected readonly hasUnappliedEdits = this.store.hasUnappliedYamlEdits;

  protected readonly lines = computed<readonly YamlLine[]>(() => {
    const yaml = this.store.generatedYaml();
    const highlighted = new Set(this.store.highlightedLines());
    const provenance = this.store.provenance();

    if (yaml === '') {
      return [];
    }

    // Only lines an element produced are focusable. Section headers and blank lines map to nothing,
    // and making them tab stops would bury the ones that do something.
    const mapped = new Set<number>();

    for (const entry of provenance) {
      for (let line = entry.startLine; line <= entry.endLine; line += 1) {
        mapped.add(line);
      }
    }

    return yaml
      .replace(/\n$/, '')
      .split('\n')
      .map((text, index) => ({
        number: index + 1,
        text,
        highlighted: highlighted.has(index + 1),
        selectable: mapped.has(index + 1),
      }));
  });

  protected readonly blockingCount = computed(() => this.findings().length);

  protected beginEditing(): void {
    this.store.beginYamlDraft();
  }

  protected onDraftInput(event: Event): void {
    this.store.updateYamlDraft((event.target as HTMLTextAreaElement).value);
  }

  protected apply(): void {
    this.store.applyYamlDraft();
  }

  protected discard(): void {
    this.store.discardYamlDraft();
  }

  protected selectLine(line: number): void {
    this.store.selectByYamlLine(line);
  }
}
