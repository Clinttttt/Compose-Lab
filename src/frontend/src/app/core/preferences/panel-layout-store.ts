import { Injectable, signal } from '@angular/core';

/** Where the simulation panel sits, or whether it is collapsed to its header. */
export type ConsoleDock = 'bottom' | 'right' | 'collapsed';

const STORAGE_KEY = 'composelab.console-dock';

const POSITIONS: readonly ConsoleDock[] = ['bottom', 'right', 'collapsed'];

/**
 * Where the tool panels sit. A preference, not application state.
 *
 * It lives in `core/` because it is persisted, and reading storage from a component is what this
 * project's frontend reference forbids. It carries nothing about an architecture, so it is safe to
 * remember across sessions in a way project work deliberately is not.
 */
@Injectable({ providedIn: 'root' })
export class PanelLayoutStore {
  private readonly dock = signal<ConsoleDock>(restore());

  readonly consoleDock = this.dock.asReadonly();

  setConsoleDock(dock: ConsoleDock): void {
    this.dock.set(dock);

    try {
      localStorage.setItem(STORAGE_KEY, dock);
    } catch {
      // Storage can be unavailable or full. A forgotten preference is not worth failing over.
    }
  }
}

function restore(): ConsoleDock {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);

    return isDock(stored) ? stored : 'bottom';
  } catch {
    return 'bottom';
  }
}

function isDock(value: string | null): value is ConsoleDock {
  return value !== null && POSITIONS.includes(value as ConsoleDock);
}
