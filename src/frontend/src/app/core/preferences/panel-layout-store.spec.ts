import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { PanelLayoutStore } from './panel-layout-store';

const STORAGE_KEY = 'composelab.console-dock';

describe('PanelLayoutStore', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('starts docked along the bottom', () => {
    expect(TestBed.inject(PanelLayoutStore).consoleDock()).toBe('bottom');
  });

  it('remembers the chosen placement', () => {
    TestBed.inject(PanelLayoutStore).setConsoleDock('right');

    expect(localStorage.getItem(STORAGE_KEY)).toBe('right');
  });

  it('restores a remembered placement', () => {
    localStorage.setItem(STORAGE_KEY, 'collapsed');

    expect(TestBed.inject(PanelLayoutStore).consoleDock()).toBe('collapsed');
  });

  /** A stored value from another version, or a hand-edited one, must not leave the layout undefined. */
  it('falls back to the bottom when the stored value is not a placement', () => {
    localStorage.setItem(STORAGE_KEY, 'floating-somewhere');

    expect(TestBed.inject(PanelLayoutStore).consoleDock()).toBe('bottom');
  });

  it('is a preference, not architecture state: nothing about a topology is stored', () => {
    TestBed.inject(PanelLayoutStore).setConsoleDock('right');

    const stored = Object.keys(localStorage).map((key) => `${key}=${localStorage.getItem(key)}`);

    expect(stored).toEqual([`${STORAGE_KEY}=right`]);
  });
});
