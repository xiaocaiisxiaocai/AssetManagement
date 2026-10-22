import { ref } from 'vue';

export interface TableColumnOption {
  key: string;
  label: string;
  /** 始终显示，例如操作列。 */
  locked?: boolean;
}

const STORAGE_PREFIX = 'assetmgmt.table-columns.';

type KeyValueStore = Pick<Storage, 'getItem' | 'removeItem' | 'setItem'>;

function browserStore(): KeyValueStore | null {
  if (typeof localStorage === 'undefined') return null;
  return localStorage;
}

export function columnIsVisible(
  columns: readonly TableColumnOption[],
  hiddenKeys: readonly string[],
  key: string,
): boolean {
  const column = columns.find((item) => item.key === key);
  if (!column || column.locked) return true;
  return !hiddenKeys.includes(key);
}

export function isLastVisibleDataColumn(
  columns: readonly TableColumnOption[],
  hiddenKeys: readonly string[],
  key: string,
): boolean {
  const hidden = new Set(hiddenKeys);
  const visible = columns.filter(
    (column) => !column.locked && !hidden.has(column.key),
  );
  return visible.length === 1 && visible[0]?.key === key;
}

/** 去掉未知键，并保证至少留一列可隐藏的数据列。 */
export function normalizeHiddenColumnKeys(
  columns: readonly TableColumnOption[],
  hiddenKeys: readonly string[],
): string[] {
  const dataKeys = columns
    .filter((column) => !column.locked)
    .map((column) => column.key);
  const allowed = new Set(dataKeys);
  const next = dataKeys.filter(
    (key) => allowed.has(key) && hiddenKeys.includes(key),
  );
  const visibleCount = dataKeys.filter((key) => !next.includes(key)).length;
  if (dataKeys.length > 0 && visibleCount === 0) {
    return next.filter((key) => key !== dataKeys[0]);
  }
  return next;
}

export function applyColumnVisibility(
  hiddenKeys: readonly string[],
  key: string,
  visible: boolean,
  columns: readonly TableColumnOption[],
): string[] {
  const current = normalizeHiddenColumnKeys(columns, hiddenKeys);
  const column = columns.find((item) => item.key === key);
  if (!column || column.locked) return current;
  if (visible) return current.filter((item) => item !== key);
  if (isLastVisibleDataColumn(columns, current, key)) return current;
  if (current.includes(key)) return current;
  return normalizeHiddenColumnKeys(columns, [...current, key]);
}

export function readHiddenColumnKeys(
  tableId: string,
  columns: readonly TableColumnOption[],
  storage: KeyValueStore | null = browserStore(),
): string[] {
  if (!storage) return [];
  try {
    const raw = storage.getItem(`${STORAGE_PREFIX}${tableId}`);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    const keys = parsed.filter((item): item is string => typeof item === 'string');
    return normalizeHiddenColumnKeys(columns, keys);
  } catch {
    return [];
  }
}

export function writeHiddenColumnKeys(
  tableId: string,
  hiddenKeys: readonly string[],
  storage: KeyValueStore | null = browserStore(),
): void {
  if (!storage) return;
  const storageKey = `${STORAGE_PREFIX}${tableId}`;
  if (hiddenKeys.length === 0) {
    storage.removeItem(storageKey);
    return;
  }
  storage.setItem(storageKey, JSON.stringify(hiddenKeys));
}

export function useTableColumns(
  tableId: string,
  columns: readonly TableColumnOption[],
) {
  const hiddenKeys = ref(readHiddenColumnKeys(tableId, columns));

  function visibleColumn(key: string) {
    return columnIsVisible(columns, hiddenKeys.value, key);
  }

  function setColumnVisible(key: string, visible: boolean) {
    hiddenKeys.value = applyColumnVisibility(
      hiddenKeys.value,
      key,
      visible,
      columns,
    );
    writeHiddenColumnKeys(tableId, hiddenKeys.value);
  }

  function resetColumns() {
    hiddenKeys.value = [];
    writeHiddenColumnKeys(tableId, []);
  }

  return { hiddenKeys, resetColumns, setColumnVisible, visibleColumn };
}
