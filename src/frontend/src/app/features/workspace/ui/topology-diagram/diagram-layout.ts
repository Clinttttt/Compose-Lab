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
export const CARD_HEIGHT = 88;
export const CARD_GAP = 16;
export const BAND_HEIGHT = 32;
export const BAND_GAP = 10;
export const BANDS_OFFSET = 28;

export interface ServiceNode {
  readonly name: string;
  readonly x: number;
  readonly y: number;
}

export type BandKind = 'network' | 'volume';

export interface BandNode {
  readonly kind: BandKind;
  readonly name: string;
  readonly y: number;
}

export interface Connector {
  readonly kind: BandKind;
  readonly service: string;
  readonly target: string;
  readonly x: number;
  readonly y1: number;
  readonly y2: number;
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

  const bandsTop = CARD_HEIGHT + BANDS_OFFSET;

  const bands: BandNode[] = [
    ...topology.networks.map((network) => ({ kind: 'network' as const, name: network.name })),
    ...topology.volumes.map((volume) => ({ kind: 'volume' as const, name: volume.name })),
  ].map((band, index) => ({ ...band, y: bandsTop + index * (BAND_HEIGHT + BAND_GAP) }));

  const centreOf = (name: string): number | null => {
    const node = services.find((service) => service.name === name);

    return node === undefined ? null : node.x + CARD_WIDTH / 2;
  };

  const bandOf = (kind: BandKind, name: string): BandNode | undefined =>
    bands.find((band) => band.kind === kind && band.name === name);

  const connectors: Connector[] = [];

  for (const service of topology.services) {
    const x = centreOf(service.name);

    if (x === null) {
      continue;
    }

    // A service is drawn once and connects to every band it belongs to. Membership is a
    // relationship, not ownership by one lane.
    for (const network of service.networks) {
      const band = bandOf('network', network);

      if (band !== undefined) {
        connectors.push({
          kind: 'network',
          service: service.name,
          target: network,
          x,
          y1: CARD_HEIGHT,
          y2: band.y + BAND_HEIGHT / 2,
        });
      }
    }

    for (const mount of service.volumes) {
      const band = bandOf('volume', mount.volume);

      if (band !== undefined) {
        connectors.push({
          kind: 'volume',
          service: service.name,
          target: mount.volume,
          x,
          y1: CARD_HEIGHT,
          y2: band.y + BAND_HEIGHT / 2,
        });
      }
    }
  }

  const width = Math.max(services.length * (CARD_WIDTH + CARD_GAP) - CARD_GAP, CARD_WIDTH * 2);

  const height =
    bands.length === 0
      ? CARD_HEIGHT
      : bandsTop + bands.length * (BAND_HEIGHT + BAND_GAP) - BAND_GAP;

  return { services, bands, connectors, width, height };
}
