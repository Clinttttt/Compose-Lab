import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  GenerateComposeResponse,
  ParseComposeResponse,
  ProjectListResponse,
  ProjectResponse,
  SaveProjectRequest,
  SimulateResponse,
  TopologyDocument,
  ValidateResponse,
} from './workspace.model';

/**
 * Talks to the API and returns typed data. Nothing else.
 *
 * Relative URLs only — `apiInterceptor` prepends the host. No error handling here: transport
 * failures belong to `errorInterceptor`, and findings about an architecture are part of a successful
 * response rather than an error.
 */
@Injectable({ providedIn: 'root' })
export class WorkspaceApi {
  private readonly http = inject(HttpClient);

  /** Cheap enough to run after an edit. */
  validate(topology: TopologyDocument): Observable<ValidateResponse> {
    return this.http.post<ValidateResponse>('/api/topology/validate', topology);
  }

  /** An explicit learner action, never automatic. */
  simulate(topology: TopologyDocument): Observable<SimulateResponse> {
    return this.http.post<SimulateResponse>('/api/topology/simulate', topology);
  }

  /** The only source of Compose YAML, and of the element-to-line provenance that goes with it. */
  generate(topology: TopologyDocument): Observable<GenerateComposeResponse> {
    return this.http.post<GenerateComposeResponse>('/api/compose/generate', topology);
  }

  parse(yaml: string): Observable<ParseComposeResponse> {
    return this.http.post<ParseComposeResponse>('/api/compose/parse', { yaml });
  }

  listProjects(): Observable<ProjectListResponse> {
    return this.http.get<ProjectListResponse>('/api/projects');
  }

  getProject(id: string): Observable<ProjectResponse> {
    return this.http.get<ProjectResponse>(`/api/projects/${id}`);
  }

  createProject(request: SaveProjectRequest): Observable<string> {
    return this.http.post<string>('/api/projects', request);
  }

  updateProject(id: string, request: SaveProjectRequest): Observable<void> {
    return this.http.put<void>(`/api/projects/${id}`, request);
  }
}
