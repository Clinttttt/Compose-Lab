import { Routes } from '@angular/router';
import { WorkspaceStore } from './workspace-store';

/**
 * The feature owns its own paths, and the store is provided here rather than at the root: it is
 * workspace state, so it lives and dies with the workspace route and every panel beneath it shares
 * the one instance.
 */
export const WORKSPACE_ROUTES: Routes = [
  {
    path: '',
    providers: [WorkspaceStore],
    loadComponent: () =>
      import('./pages/workspace-page/workspace-page').then((m) => m.WorkspacePage),
  },
];
