import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Icon } from '@shared/icon/icon';
import { WorkspaceStore } from '../../workspace-store';
import { ServiceDocument } from '../../workspace.model';

/**
 * The palette adds things to the architecture.
 *
 * Templates carry the defaults that teach: a database arrives unpublished, because service-to-service
 * traffic never goes through the host, and the API arrives with a connection string so its intent to
 * reach the database is explicit rather than guessed at.
 */
@Component({
  selector: 'app-component-palette',
  imports: [Icon],
  templateUrl: './component-palette.html',
  styleUrl: './component-palette.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComponentPalette {
  private readonly store = inject(WorkspaceStore);

  protected readonly topology = this.store.authoredTopology;

  protected addReferenceArchitecture(): void {
    this.store.declareNetwork('backend');
    this.store.declareVolume('postgres-data');
    this.store.addService(this.aspNetApi());
    this.store.addService(this.postgres());
  }

  protected addService(kind: 'api' | 'postgres' | 'redis' | 'generic'): void {
    const service =
      kind === 'api'
        ? this.aspNetApi()
        : kind === 'postgres'
          ? this.postgres()
          : kind === 'redis'
            ? this.redis()
            : this.generic();

    this.store.addService({ ...service, name: this.uniqueName(service.name) });
  }

  protected addNetwork(): void {
    this.store.declareNetwork(this.uniqueNetworkName('backend'));
  }

  protected addVolume(): void {
    this.store.declareVolume(this.uniqueVolumeName('data'));
  }

  private aspNetApi(): ServiceDocument {
    return {
      name: 'api',
      build: './Api',
      // Published: this is the one thing that is meant to be reachable from outside.
      ports: [{ hostPort: 8080, containerPort: 8080 }],
      networks: this.defaultNetwork(),
      volumes: [],
      dependsOn: [{ service: 'database', condition: 'service_started' }],
      environment: { ConnectionStrings__Database: 'Host=database;Database=app' },
    };
  }

  private postgres(): ServiceDocument {
    return {
      name: 'database',
      image: 'postgres:18',
      // Not published on purpose: the API reaches it by name on a shared network.
      ports: [],
      networks: this.defaultNetwork(),
      volumes: this.topology().volumes.some((volume) => volume.name === 'postgres-data')
        ? // postgres:18 wants a single mount at /var/lib/postgresql and places its data in a
          // subdirectory itself. The classic /var/lib/postgresql/data — correct for 17 and earlier —
          // makes an 18 container exit on startup, so the template must not teach it.
          [{ volume: 'postgres-data', path: '/var/lib/postgresql' }]
        : [],
      dependsOn: [],
      environment: { POSTGRES_PASSWORD: 'development' },
    };
  }

  private redis(): ServiceDocument {
    return {
      name: 'cache',
      image: 'redis:8',
      ports: [],
      networks: this.defaultNetwork(),
      volumes: [],
      dependsOn: [],
      environment: {},
    };
  }

  private generic(): ServiceDocument {
    return {
      name: 'service',
      image: 'alpine:3',
      ports: [],
      networks: this.defaultNetwork(),
      volumes: [],
      dependsOn: [],
      environment: {},
    };
  }

  /** Attaches to a declared network when one exists, and otherwise leaves it to Compose. */
  private defaultNetwork(): string[] {
    const first = this.topology().networks.at(0);

    return first === undefined ? [] : [first.name];
  }

  private uniqueName(base: string): string {
    return this.unique(
      base,
      this.topology().services.map((service) => service.name),
    );
  }

  private uniqueNetworkName(base: string): string {
    return this.unique(
      base,
      this.topology().networks.map((network) => network.name),
    );
  }

  private uniqueVolumeName(base: string): string {
    return this.unique(
      base,
      this.topology().volumes.map((volume) => volume.name),
    );
  }

  private unique(base: string, taken: readonly string[]): string {
    if (!taken.includes(base)) {
      return base;
    }

    let suffix = 2;

    while (taken.includes(`${base}-${suffix}`)) {
      suffix += 1;
    }

    return `${base}-${suffix}`;
  }
}
