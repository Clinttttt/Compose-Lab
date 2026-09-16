import { computed, Injectable, signal } from '@angular/core';

/**
 * Transport-level failures, in one place for the shell to render.
 *
 * Deliberately minimal. This reports that a request could not be completed — a network drop, a 500,
 * a rejected payload. It never carries anything about whether an architecture is sound: findings,
 * severities, and explanations come from the backend's own responses and belong to the panels that
 * asked for them.
 */
@Injectable({ providedIn: 'root' })
export class Notifier {
  private readonly message = signal<string | null>(null);

  readonly currentError = computed(() => this.message());

  error(message: string): void {
    this.message.set(message);
  }

  clear(): void {
    this.message.set(null);
  }
}
