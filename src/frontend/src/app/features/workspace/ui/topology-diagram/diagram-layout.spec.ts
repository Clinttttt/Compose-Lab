import { describe, expect, it } from 'vitest';
import { BAND_HEIGHT, CARD_HEIGHT, CARD_WIDTH, deriveLayout } from './diagram-layout';
import { ServiceDocument, TopologyDocument } from '../../workspace.model';

function service(name: string, patch: Partial<ServiceDocument> = {}): ServiceDocument {
  return {
    name,
    image: `${name}:1`,
    ports: [],
    networks: [],
    volumes: [],
    dependsOn: [],
    environment: {},
    ...patch,
  };
}

function topology(patch: Partial<TopologyDocument> = {}): TopologyDocument {
  return { services: [], networks: [], volumes: [], ...patch };
}

describe('deriveLayout', () => {
  it('places services in a row without overlapping', () => {
    const layout = deriveLayout(topology({ services: [service('api'), service('database')] }));

    expect(layout.services.map((node) => node.name)).toEqual(['api', 'database']);
    expect(layout.services[1].x).toBeGreaterThanOrEqual(layout.services[0].x + CARD_WIDTH);
  });

  it('renders a service once even when it belongs to several networks', () => {
    const layout = deriveLayout(
      topology({
        services: [service('app', { networks: ['frontend', 'backend'] })],
        networks: [{ name: 'frontend' }, { name: 'backend' }],
      }),
    );

    expect(layout.services).toHaveLength(1);

    // Membership is a relationship: one card, one connector per network it joins.
    expect(layout.connectors.filter((edge) => edge.service === 'app')).toHaveLength(2);
    expect(layout.connectors.map((edge) => edge.target).sort()).toEqual(['backend', 'frontend']);
  });

  it('connects a service to the volume it mounts, distinctly from networks', () => {
    const layout = deriveLayout(
      topology({
        services: [
          service('database', {
            networks: ['backend'],
            volumes: [{ volume: 'data', path: '/var/lib/postgresql/data' }],
          }),
        ],
        networks: [{ name: 'backend' }],
        volumes: [{ name: 'data' }],
      }),
    );

    expect(layout.connectors.filter((edge) => edge.kind === 'network')).toHaveLength(1);
    expect(layout.connectors.filter((edge) => edge.kind === 'volume')).toHaveLength(1);
  });

  it('draws no connector to a network that is referenced but not declared', () => {
    const layout = deriveLayout(
      topology({ services: [service('api', { networks: ['nowhere'] })] }),
    );

    expect(layout.connectors).toHaveLength(0);
  });

  it('is a pure function of the topology, so the same architecture always lays out the same way', () => {
    const input = topology({
      services: [service('api', { networks: ['backend'] })],
      networks: [{ name: 'backend' }],
    });

    expect(deriveLayout(input)).toEqual(deriveLayout(input));
  });

  it('reserves room for every band beneath the service row', () => {
    const layout = deriveLayout(
      topology({ networks: [{ name: 'backend' }], volumes: [{ name: 'data' }] }),
    );

    expect(layout.bands).toHaveLength(2);
    expect(layout.bands[0].y).toBeGreaterThanOrEqual(CARD_HEIGHT);
    expect(layout.bands[1].y).toBeGreaterThanOrEqual(layout.bands[0].y + BAND_HEIGHT);
  });
});
