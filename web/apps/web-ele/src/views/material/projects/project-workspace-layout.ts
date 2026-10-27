/** 工作台各区块占比（0–1），拖拽分隔条后按用户浏览器记住。 */
export interface WorkspaceLayout {
  /** 料件清单占竖向分隔容器的比例。 */
  materials: number;
  /** 测试阶段与报告占竖向分隔容器的比例。 */
  testing: number;
  /** 流转审批占底部行的比例，其余给落地跟进。 */
  flows: number;
}

export const DEFAULT_WORKSPACE_LAYOUT: Readonly<WorkspaceLayout> = {
  flows: 0.62,
  materials: 0.24,
  testing: 0.32,
};

const STORAGE_KEY = 'assetmgmt.project-workspace-layout';
const MIN_RATIO = 0.1;
/** 竖向两块合计上限，给底部行留出空间。 */
const MAX_VERTICAL_TOTAL = 0.8;

type KeyValueStore = Pick<Storage, 'getItem' | 'setItem'>;

function browserStore(): KeyValueStore | null {
  if (typeof localStorage === 'undefined') return null;
  return localStorage;
}

function clampRatio(value: unknown, fallback: number, max: number) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return fallback;
  return Math.min(Math.max(value, MIN_RATIO), max);
}

export function normalizeWorkspaceLayout(value: unknown): WorkspaceLayout {
  const source =
    value && typeof value === 'object'
      ? (value as Record<string, unknown>)
      : {};
  const materials = clampRatio(
    source.materials,
    DEFAULT_WORKSPACE_LAYOUT.materials,
    MAX_VERTICAL_TOTAL - MIN_RATIO,
  );
  const testing = clampRatio(
    source.testing,
    DEFAULT_WORKSPACE_LAYOUT.testing,
    MAX_VERTICAL_TOTAL - materials,
  );
  const flows = clampRatio(
    source.flows,
    DEFAULT_WORKSPACE_LAYOUT.flows,
    1 - MIN_RATIO,
  );
  return { flows, materials, testing };
}

export function readWorkspaceLayout(
  storage: KeyValueStore | null = browserStore(),
): WorkspaceLayout {
  if (!storage) return { ...DEFAULT_WORKSPACE_LAYOUT };
  try {
    const raw = storage.getItem(STORAGE_KEY);
    if (!raw) return { ...DEFAULT_WORKSPACE_LAYOUT };
    return normalizeWorkspaceLayout(JSON.parse(raw));
  } catch {
    return { ...DEFAULT_WORKSPACE_LAYOUT };
  }
}

export function writeWorkspaceLayout(
  layout: WorkspaceLayout,
  storage: KeyValueStore | null = browserStore(),
): void {
  if (!storage) return;
  try {
    storage.setItem(
      STORAGE_KEY,
      JSON.stringify(normalizeWorkspaceLayout(layout)),
    );
  } catch {
    // 存储不可用（隐私模式、配额已满）时只保留本次会话内的尺寸
  }
}

/** 由分隔条回传的像素尺寸计算第 index 块的占比。 */
export function ratioAt(pxSizes: readonly number[], index: number) {
  const total = pxSizes.reduce((sum, size) => sum + size, 0);
  const size = pxSizes[index];
  if (!(total > 0) || size === undefined) return undefined;
  return size / total;
}

export function toPercent(ratio: number) {
  return `${Math.round(ratio * 10_000) / 100}%`;
}
