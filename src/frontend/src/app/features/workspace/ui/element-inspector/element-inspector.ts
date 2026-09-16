import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon } from '@shared/icon/icon';
import { WorkspaceStore } from '../../workspace-store';

/**
 * The inspector edits the one working topology through the store.
 *
 * It holds no editable copy of its own: every field reads the current topology and every change is a
 * patch sent straight back. It is also where relationships are available as text, which is what lets
 * the diagram's connector lines stay decorative.
 */
@Component({
  selector: 'app-element-inspector',
  imports: [Icon],
  templateUrl: './element-inspector.html',
  styleUrl: './element-inspector.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ElementInspector {
  private readonly store = inject(WorkspaceStore);

  protected readonly topology = this.store.authoredTopology;
  protected readonly selected = this.store.selectedElement;
  protected readonly service = this.store.selectedService;

  /** Which services attach to the selected network, or mount the selected volume. */
  protected readonly relatedServices = computed<readonly string[]>(() => {
    const selected = this.selected();

    if (selected === null || selected.ownerService !== null) {
      return [];
    }

    if (selected.kind === 'network') {
      return this.topology()
        .services.filter((service) => service.networks.includes(selected.name))
        .map((service) => service.name);
    }

    if (selected.kind === 'volume') {
      return this.topology()
        .services.filter((service) =>
          service.volumes.some((mount) => mount.volume === selected.name),
        )
        .map((service) => service.name);
    }

    return [];
  });

  protected readonly environmentEntries = computed<readonly { key: string; value: string }[]>(
    () => {
      const service = this.service();

      return service === null
        ? []
        : Object.entries(service.environment).map(([key, value]) => ({ key, value }));
    },
  );

  protected setImage(event: Event): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, { image: this.valueOf(event) || null });
    }
  }

  protected setBuild(event: Event): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, { build: this.valueOf(event) || null });
    }
  }

  protected addPort(): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, {
        ports: [...service.ports, { hostPort: 8080, containerPort: 8080 }],
      });
    }
  }

  protected setHostPort(index: number, event: Event): void {
    const service = this.service();
    const raw = this.valueOf(event);

    if (service !== null) {
      this.store.updateService(service.name, {
        ports: service.ports.map((port, position) =>
          position === index ? { ...port, hostPort: raw === '' ? null : Number(raw) } : port,
        ),
      });
    }
  }

  protected setContainerPort(index: number, event: Event): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, {
        ports: service.ports.map((port, position) =>
          position === index ? { ...port, containerPort: Number(this.valueOf(event)) } : port,
        ),
      });
    }
  }

  protected removePort(index: number): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, {
        ports: service.ports.filter((_port, position) => position !== index),
      });
    }
  }

  protected toggleNetwork(network: string): void {
    const service = this.service();

    if (service === null) {
      return;
    }

    const attached = service.networks.includes(network);

    this.store.updateService(service.name, {
      networks: attached
        ? service.networks.filter((name) => name !== network)
        : [...service.networks, network],
    });
  }

  protected addMount(event: Event): void {
    const service = this.service();
    const volume = this.valueOf(event);

    if (service === null || volume === '') {
      return;
    }

    if (!service.volumes.some((mount) => mount.volume === volume)) {
      this.store.updateService(service.name, {
        volumes: [...service.volumes, { volume, path: '/data' }],
      });
    }
  }

  protected setMountPath(index: number, event: Event): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, {
        volumes: service.volumes.map((mount, position) =>
          position === index ? { ...mount, path: this.valueOf(event) } : mount,
        ),
      });
    }
  }

  protected removeMount(index: number): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, {
        volumes: service.volumes.filter((_mount, position) => position !== index),
      });
    }
  }

  protected setEnvironmentValue(key: string, event: Event): void {
    const service = this.service();

    if (service !== null) {
      this.store.updateService(service.name, {
        environment: { ...service.environment, [key]: this.valueOf(event) },
      });
    }
  }

  protected removeEnvironment(key: string): void {
    const service = this.service();

    if (service === null) {
      return;
    }

    const remaining = Object.fromEntries(
      Object.entries(service.environment).filter(([existing]) => existing !== key),
    );

    this.store.updateService(service.name, { environment: remaining });
  }

  protected addEnvironment(keyInput: HTMLInputElement): void {
    const service = this.service();
    const key = keyInput.value.trim();

    if (service === null || key === '') {
      return;
    }

    this.store.updateService(service.name, {
      environment: { ...service.environment, [key]: '' },
    });

    keyInput.value = '';
  }

  protected removeService(): void {
    const service = this.service();

    if (service !== null) {
      this.store.removeService(service.name);
    }
  }

  protected removeSelectedResource(): void {
    const selected = this.selected();

    if (selected === null) {
      return;
    }

    if (selected.kind === 'network') {
      this.store.removeNetwork(selected.name);
    }

    if (selected.kind === 'volume') {
      this.store.removeVolume(selected.name);
    }

    this.store.select(null);
  }

  private valueOf(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
