import { describe, expect, it } from 'vitest';

import {
  applyColumnVisibility,
  columnIsVisible,
  normalizeHiddenColumnKeys,
  readHiddenColumnKeys,
  writeHiddenColumnKeys,
} from './column-visibility';

const columns = [
  { key: 'no', label: '编号' },
  { key: 'name', label: '名称' },
  { key: 'remark', label: '备注' },
  { key: 'actions', label: '操作', locked: true },
];

function memoryStore() {
  const data = new Map<string, string>();
  return {
    data,
    getItem: (key: string) => data.get(key) ?? null,
    removeItem: (key: string) => data.delete(key),
    setItem: (key: string, value: string) => data.set(key, value),
  };
}

describe('表格列显示', () => {
  it('锁定列始终显示，隐藏列不显示', () => {
    expect(columnIsVisible(columns, ['remark'], 'remark')).toBe(false);
    expect(columnIsVisible(columns, ['actions'], 'actions')).toBe(true);
    expect(columnIsVisible(columns, ['name'], 'no')).toBe(true);
  });

  it('不能隐藏最后一列数据', () => {
    const hidden = applyColumnVisibility(
      ['name', 'remark'],
      'no',
      false,
      columns,
    );
    expect(hidden).toEqual(['name', 'remark']);
    expect(columnIsVisible(columns, hidden, 'no')).toBe(true);
  });

  it('恢复显示并忽略未知键', () => {
    expect(applyColumnVisibility(['name', 'ghost'], 'name', true, columns)).toEqual(
      [],
    );
    expect(normalizeHiddenColumnKeys(columns, ['ghost', 'remark'])).toEqual([
      'remark',
    ]);
  });

  it('损坏的全隐藏记录会留下第一列', () => {
    expect(
      normalizeHiddenColumnKeys(columns, ['no', 'name', 'remark', 'actions']),
    ).toEqual(['name', 'remark']);
  });

  it('按表格编号写入本机并在清空时删除', () => {
    const storage = memoryStore();
    writeHiddenColumnKeys('asset-list', ['remark'], storage);
    expect(readHiddenColumnKeys('asset-list', columns, storage)).toEqual([
      'remark',
    ]);
    expect(readHiddenColumnKeys('other', columns, storage)).toEqual([]);

    writeHiddenColumnKeys('asset-list', [], storage);
    expect(storage.data.has('assetmgmt.table-columns.asset-list')).toBe(false);
  });

  it('非法存储内容按全部显示处理', () => {
    const storage = memoryStore();
    storage.setItem('assetmgmt.table-columns.asset-list', '{');
    expect(readHiddenColumnKeys('asset-list', columns, storage)).toEqual([]);
    storage.setItem('assetmgmt.table-columns.asset-list', '"remark"');
    expect(readHiddenColumnKeys('asset-list', columns, storage)).toEqual([]);
  });
});
