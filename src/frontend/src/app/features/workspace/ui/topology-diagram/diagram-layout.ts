import { TopologyDocument } from '../../workspace.model';

/**
 * The diagram's geometry, derived entirely from the topology.
 *
 * Fixed geometry rather than measured DOM: the layout is a function of the architecture, so the
 * connector coordinates can be computed alongside it. That removes the resize-observation dance and
 * makes the whole thing deterministic and testable.
 *
 * No coordinates are persisted anywhere. Move a service in the topology and the picture follows;
 * there is nothing to drag and nothing to keep in sync.
 */
export const CARD_WIDTH = 168;
/** Tall enough for the five rows a card can carry: name, source, ports, memberships, and a flag. */
export const CARD_HEIGHT = 104;
export const CARD_GAP = 16;
export const BAND_HEIGHT = 32;
export const BAND_GAP = 10;
export const BANDS_OFFSET = 28;

/** The network Compose creates for a project when a service declares none. */
export const DEFAULT_NETWORK = 'default';

export interface ServiceNode {
  readonly name: string;
  readonly x: number;
  readonly y: number;
}

export type BandKind = 'network' | 'volume';

/**
 * Whether the author wrote this band down, or whether Compose supplies it.
 *
 * An implied band is view-only. It exists so the diagram shows the network the simulator will talk
 * about, and it is never written back into the topology document — Compose's behaviour is not the
 * learner's configuration.
 */
export type BandOrigin = 'declared' | 'implied';

export interface BandNode {
  readonly kind: BandKind;
  readonly name: string;
  readonly y: number;
  readonly origin: BandOrigin;
}

export interface Connector {
  readonly kind: BandKind;
  readonly service: string;
  readonly target: string;
  readonly x: number;
  readonly y1: number;
  readonly y2: number;
  /** True when the attachment is Compose's doing rather than something the author listed. */
  readonly implied: boolean;
}

export interface DiagramLayout {
  readonly services: readonly ServiceNode[];
  readonly bands: readonly BandNode[];
  readonly connectors: readonly Connector[];
  readonly width: number;
  readonly height: number;
}

export function deriveLayout(topology: TopologyDocument): DiagramLayout {
  const services: ServiceNode[] = topology.services.map((service, index) => ({
    name: service.name,
    x: index * (CARD_WIDTH + CARD_GAP),
    y: 0,
  }));

  // A service that names no network joins the one Compose creates. That is the single most common
  // beginner Compose file, and a diagram that omits the network would be describing a different
  // architecture from the one the simulator reports on.
  const impliedAttachments = topology.services.some((service) => service.networks.length === 0);

  const defaultIsDeclared = topology.networks.some((network) => network.name === DEFAULT_NETWORK);

  const networkBands: { kind: BandKind; name: string; origin: BandOrigin }[] =
    topology.networks.map((network) => ({
      kind: 'network',
      name: network.name,
      origin: 'declared',
    }));

  // An explicitly declared `default` keeps its declaration; only the attachment to it is implied.
  if (impliedAttachments && !defaultIsDeclared) {
    networkBands.push({ kind: 'network', name: DEFAULT_NETWORK, origin: 'implied' });
  }

  const bandsTop = CARD_HEIGHT + BANDS_OFFSET;

  const bands: BandNode[] = [
    ...networkBands,
    ...topology.volumes.map((volume) => ({
      kind: 'volume' as const,
      name: volume.name,
      origin: 'declared' as const,
    })),
  ].map((band, index) => ({ ...band, y: bandsTop + index * (BAND_HEIGHT + BAND_GAP) }));

  const centreOf = (name: string): number | null => {
    const node = services.find((service) => service.name === name);

    return node === undefined ? null : node.x + CARD_WIDTH / 2;
  };

  const bandOf = (kind: BandKind, name: string): BandNode | undefined =>
    bands.find((band) => band.kind === kind && band.name === name);

  const connectors: Connector[] = [];

  const connect = (
    kind: BandKind,
    service: string,
    target: string,
    x: number,
    implied: boolean,
  ): void => {
    const band = bandOf(kind, target);

    if (band !== undefined) {
      connectors.push({
        kind,
        service,
        target,
        x,
        y1: CARD_HEIGHT,
        y2: band.y + BAND_HEIGHT / 2,
        implied,
      });
    }
  };

  for (const service of topology.services) {
    const x = centreOf(service.name);

    if (x === null) {
      continue;
    }

    // A service is drawn once and connects to every band it belongs to. Membership is a
    // relationship, not ownership by one lane.
    if (service.networks.length === 0) {
      connect('network', service.name, DEFAULT_NETWORK, x, true);
    } else {
      for (const network of service.networks) {
        connect('network', service.name, network, x, false);
      }
    }

    for (const mount of service.volumes) {
      connect('volume', service.name, mount.volume, x, false);
    }
  }

  const width = Math.max(services.length * (CARD_WIDTH + CARD_GAP) - CARD_GAP, CARD_WIDTH * 2);

  const height =
    bands.length === 0
      ? CARD_HEIGHT
      : bandsTop + bands.length * (BAND_HEIGHT + BAND_GAP) - BAND_GAP;

  return { services, bands, connectors, width, height };
}
