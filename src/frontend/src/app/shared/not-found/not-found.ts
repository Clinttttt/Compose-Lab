import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="not-found">
      <h1>That page does not exist.</h1>
      <a routerLink="/workspace">Back to the workspace</a>
    </section>
  `,
  styles: `
    .not-found {
      display: grid;
      gap: var(--space-3);
      padding: var(--space-6);
    }

    a {
      color: var(--accent);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFound {}
