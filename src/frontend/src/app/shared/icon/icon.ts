import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type IconName =
  | 'service'
  | 'network'
  | 'volume'
  | 'simulate'
  | 'validate'
  | 'add'
  | 'remove'
  | 'save'
  | 'yaml'
  | 'warning'
  | 'error'
  | 'information'
  | 'undo'
  | 'redo'
  | 'dock-bottom'
  | 'dock-right'
  | 'collapse';

/**
 * The icon set, inlined.
 *
 * Twelve Lucide glyphs copied as SVG rather than pulled from a runtime package: it keeps the set
 * small and consistent by construction, ships no unused payload, and adds no dependency. Icons are
 * decorative here — every one sits beside a text label, so `aria-hidden` is correct and nothing is
 * communicated by the glyph alone.
 */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      xmlns="http://www.w3.org/2000/svg"
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.75"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @switch (name()) {
        @case ('service') {
          <rect x="2" y="2" width="20" height="8" rx="2" />
          <rect x="2" y="14" width="20" height="8" rx="2" />
          <path d="M6 6h.01M6 18h.01" />
        }
        @case ('network') {
          <rect x="9" y="2" width="6" height="6" rx="1" />
          <rect x="2" y="16" width="6" height="6" rx="1" />
          <rect x="16" y="16" width="6" height="6" rx="1" />
          <path d="M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3M12 12V8" />
        }
        @case ('volume') {
          <ellipse cx="12" cy="5" rx="9" ry="3" />
          <path d="M3 5v14a9 3 0 0 0 18 0V5M3 12a9 3 0 0 0 18 0" />
        }
        @case ('simulate') {
          <path d="M6 3l14 9-14 9V3z" />
        }
        @case ('validate') {
          <circle cx="12" cy="12" r="10" />
          <path d="m9 12 2 2 4-4" />
        }
        @case ('add') {
          <path d="M5 12h14M12 5v14" />
        }
        @case ('remove') {
          <path
            d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"
          />
          <path d="M10 11v6M14 11v6" />
        }
        @case ('save') {
          <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
          <path d="M17 21v-8H7v8M7 3v5h8" />
        }
        @case ('yaml') {
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <path d="M14 2v6h6M10 13l-2 2 2 2M14 17l2-2-2-2" />
        }
        @case ('warning') {
          <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3" />
          <path d="M12 9v4M12 17h.01" />
        }
        @case ('error') {
          <circle cx="12" cy="12" r="10" />
          <path d="m15 9-6 6M9 9l6 6" />
        }
        @case ('information') {
          <circle cx="12" cy="12" r="10" />
          <path d="M12 16v-4M12 8h.01" />
        }
        @case ('undo') {
          <path d="M9 14 4 9l5-5" />
          <path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H11" />
        }
        @case ('redo') {
          <path d="m15 14 5-5-5-5" />
          <path d="M20 9H9.5a5.5 5.5 0 0 0 0 11H13" />
        }
        @case ('dock-bottom') {
          <rect x="3" y="3" width="18" height="18" rx="2" />
          <path d="M3 15h18" />
        }
        @case ('dock-right') {
          <rect x="3" y="3" width="18" height="18" rx="2" />
          <path d="M15 3v18" />
        }
        @case ('collapse') {
          <path d="m7 15 5 5 5-5M7 9l5-5 5 5" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      align-items: center;
    }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();

  readonly size = input(14);
}
