import { describe, expect, it } from 'vitest';

import { buildAssetRowActionAccess, canRunAvailableAssetAction } from './asset-row-actions';

describe('资产清单行操作权限', () => {
  it('未授予 asset:delete 时不允许显示删除操作', () => {
    const permissions = new Set(['asset:view', 'asset:edit', 'approval:create']);
    const access = buildAssetRowActionAccess((codes) => codes.some((code) => permissions.has(code)));

    expect(access.canDelete).toBe(false);
    expect(access.canEdit).toBe(true);
    expect(access.canBorrow).toBe(true);
    expect(access.canCreate).toBe(false);
  });

  it('授予 asset:delete 后允许显示删除操作', () => {
    const permissions = new Set(['asset:delete']);
    const access = buildAssetRowActionAccess((codes) => codes.some((code) => permissions.has(code)));

    expect(access.canDelete).toBe(true);
  });

  it('顶部导入导出和新增资产使用独立权限码', () => {
    const permissions = new Set(['asset:create', 'asset:export']);
    const access = buildAssetRowActionAccess((codes) => codes.some((code) => permissions.has(code)));

    expect(access.canCreate).toBe(true);
    expect(access.canExport).toBe(true);
    expect(access.canImport).toBe(false);
  });
});

describe('资产状态操作', () => {
  it('只有未删除的在库资产允许发起借用、转让或删除', () => {
    expect(canRunAvailableAssetAction({ isDeleted: false, status: 0 })).toBe(true);
    expect(canRunAvailableAssetAction({ isDeleted: false, status: 1 })).toBe(false);
    expect(canRunAvailableAssetAction({ isDeleted: true, status: 0 })).toBe(false);
  });
});
