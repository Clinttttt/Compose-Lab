/**
 * The API contracts, exactly as the backend serialises them.
 *
 * These mirror `Domain/Topology/Document/TopologyDocument` and the four engine responses. Nothing
 * here is a convenience shape invented for the UI: a parallel model would have to be reconciled
 * later, and the whole point of one topology contract is that there is nothing to reconcile.
 */

// --- The authored topology: what the learner wrote, never a normalized one ---

export interface TopologyDocument {
  services: ServiceDocument[];
  networks: NetworkDocument[];
  volumes: VolumeDocument[];
}

export interface ServiceDocument {
  name: string;
  image?: string | null;
  build?: string | null;
  /** Empty means the service is not published to the host. */
  ports: PortDocument[];
  networks: string[];
  volumes: VolumeMountDocument[];
  dependsOn: DependencyDocument[];
  environment: Record<string, string>;
  /** Absent when no healthcheck is declared. */
  healthCheck?: HealthCheckDocument | null;
}

export interface PortDocument {
  /** Absent means Docker assigns the host port. Present means the author chose it. */
  hostPort?: number | null;
  containerPort: number;
  protocol?: PortProtocol | null;
}

export type PortProtocol = 'tcp' | 'udp';

export interface VolumeMountDocument {
  volume: string;
  path: string;
}

export interface DependencyDocument {
  service: string;
  condition?: DependencyCondition | null;
}

export type DependencyCondition = 'service_started' | 'service_healthy';

export interface HealthCheckDocument {
  form: HealthCheckForm;
  test: string[];
}

export type HealthCheckForm = 'disabled' | 'shell' | 'command' | 'command_shell';

export interface NetworkDocument {
  name: string;
  composeName?: string | null;
}

export interface VolumeDocument {
  name: string;
  composeName?: string | null;
}

// --- Shared vocabulary: an element reference points at one part of a topology ---

export type ElementKind =
  'service' | 'network' | 'volume' | 'port_mapping' | 'dependency' | 'environment_variable';

/**
 * `ownerService` separates a declaration from a use: a network with no owner is the top-level
 * declaration, the same network with an owner is one service's attachment to it.
 */
export interface ElementReference {
  kind: ElementKind;
  name: string;
  ownerService: string | null;
  detail: string | null;
}

export type IssueSeverity = 'information' | 'warning' | 'error';

/** A finding and its explanation. The words come from the backend; Angular only lays them out. */
export interface ArchitectureIssue {
  code: string;
  severity: IssueSeverity;
  elements: ElementReference[];
  whatHappened: string;
  why: string;
  architectureBehavior: string;
  suggestedFix: string;
}

// --- POST /api/topology/validate ---

export interface ValidateResponse {
  isValid: boolean;
  /** False when structural faults stopped the check, so the issue list is not exhaustive. */
  isComplete: boolean;
  issues: ArchitectureIssue[];
}

// --- POST /api/topology/simulate ---

export interface SimulateResponse {
  succeeded: boolean;
  completed: boolean;
  startOrder: string[];
  events: SimulationEvent[];
  issues: ArchitectureIssue[];
  reachability: ReachabilityPair[];
  inferredConnections: InferredConnection[];
}

export interface SimulationEvent {
  step: number;
  phase: SimulationPhase;
  code: string;
  severity: IssueSeverity;
  elements: ElementReference[];
  message: string;
}

export type SimulationPhase =
  | 'normalization'
  | 'structural_validation'
  | 'resource_creation'
  | 'dependency_ordering'
  | 'service_start'
  | 'reachability'
  | 'completion';

export interface ReachabilityPair {
  serviceA: string;
  serviceB: string;
  canCommunicate: boolean;
  sharedNetworks: string[];
}

export interface InferredConnection {
  fromService: string;
  toService: string;
  environmentKey: string;
  host: string;
}

// --- POST /api/compose/generate ---

export interface GenerateComposeResponse {
  yaml: string;
  provenance: ProvenanceEntry[];
}

/** One-based, inclusive. The frontend never computes these. */
export interface ProvenanceEntry {
  element: ElementReference;
  startLine: number;
  endLine: number;
}

// --- POST /api/compose/parse ---

export interface ParseComposeResponse {
  /** False means nothing may be applied: any unmodeled or unknown key blocks the whole file. */
  canApply: boolean;
  topology: TopologyDocument | null;
  findings: ComposeFinding[];
}

export interface ComposeFinding {
  code: ComposeFindingCode;
  path: string;
  line: number;
  column: number;
  message: string;
  suggestion: string;
}

export type ComposeFindingCode =
  | 'compose.syntax_error'
  | 'compose.unexpected_shape'
  | 'compose.key_not_modeled'
  | 'compose.unknown_key'
  | 'compose.value_not_modeled';

// --- Projects ---

export interface ProjectSummary {
  id: string;
  name: string;
  topologySchemaVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectListResponse {
  projects: ProjectSummary[];
}

export interface ProjectResponse {
  id: string;
  name: string;
  topology: TopologyDocument;
  topologySchemaVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface SaveProjectRequest {
  name: string;
  topology: TopologyDocument;
}

export function emptyTopology(): TopologyDocument {
  return { services: [], networks: [], volumes: [] };
}

/** Two element references point at the same thing when all four fields agree. */
export function sameElement(left: ElementReference, right: ElementReference): boolean {
  return (
    left.kind === right.kind &&
    left.name === right.name &&
    left.ownerService === right.ownerService &&
    left.detail === right.detail
  );
}
