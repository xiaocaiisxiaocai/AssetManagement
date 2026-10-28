import { describe, expect, it } from 'vitest';

import {
  collapsedSectionPanelSize,
  DEFAULT_WORKSPACE_LAYOUT,
  ratioAt,
  readWorkspaceLayout,
  toPercent,
  writeWorkspaceLayout,
} from './project-workspace-layout';

function memoryStore(initial?: string) {
  const data = new Map<string, string>();
  if (initial !== undefined) {
    data.set('assetmgmt.project-workspace-layout', initial);
  }
  return {
    getItem: (key: string) => data.get(key) ?? null,
    setItem: (key: string, value: string) => {
      data.set(key, value);
    },
  };
}

describe('项目工作台布局记忆', () => {
  it('无记录或记录损坏时使用默认占比', () => {
    expect(readWorkspaceLayout(memoryStore())).toEqual(
      DEFAULT_WORKSPACE_LAYOUT,
    );
    expect(readWorkspaceLayout(memoryStore('{bad'))).toEqual(
      DEFAULT_WORKSPACE_LAYOUT,
    );
    expect(readWorkspaceLayout(null)).toEqual(DEFAULT_WORKSPACE_LAYOUT);
  });

  it('越界占比被收回，竖向两块给底部行留出空间', () => {
    const layout = readWorkspaceLayout(
      memoryStore(JSON.stringify({ flows: 2, materials: 0.6, testing: 0.5 })),
    );
    expect(layout.materials).toBe(0.6);
    expect(layout.testing).toBeCloseTo(0.2);
    expect(layout.flows).toBe(0.9);
  });

  it('写入后可读回', () => {
    const store = memoryStore();
    writeWorkspaceLayout({ flows: 0.5, materials: 0.3, testing: 0.3 }, store);
    expect(readWorkspaceLayout(store)).toEqual({
      flows: 0.5,
      materials: 0.3,
      testing: 0.3,
    });
  });

  it('按像素尺寸计算占比并转成百分比', () => {
    expect(ratioAt([200, 300, 500], 1)).toBe(0.3);
    expect(ratioAt([0, 0], 0)).toBeUndefined();
    expect(ratioAt([100], 3)).toBeUndefined();
    expect(toPercent(0.2468)).toBe('24.68%');
  });

  it('收起后面板等于区块高度加底部间距', () => {
    expect(collapsedSectionPanelSize(50)).toBe(55);
    expect(collapsedSectionPanelSize(72.2)).toBe(78);
  });
});
