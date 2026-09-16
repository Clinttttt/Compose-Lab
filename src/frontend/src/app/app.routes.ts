import { Routes } from '@angular/router';

/**
 * `app.routes.ts` names the prefix; the feature names everything under it.
 */
export const routes: Routes = [
  {
    path: 'workspace',
    loadChildren: () =>
      import('@features/workspace/workspace.routes').then((m) => m.WORKSPACE_ROUTES),
  },
  { path: '', redirectTo: 'workspace', pathMatch: 'full' },
  {
    path: '**',
    loadComponent: () => import('@shared/not-found/not-found').then((m) => m.NotFound),
  },
];
