import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Notifier } from '@core/notifications/notifier';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly notifier = inject(Notifier);

  protected readonly transportError = this.notifier.currentError;

  protected dismissError(): void {
    this.notifier.clear();
  }
}
