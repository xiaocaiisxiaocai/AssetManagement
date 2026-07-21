import { describe, expect, it } from 'vitest';

import {
  buildAssetRowActionAccess,
  canBorrowAvailableAsset,
  canRunAvailableAssetAction,
  canTransferAvailableAsset,
} from './asset-row-actions';

describe('资产清单行操作权限', () => {
  it('未授予 asset:delete 时不允许显示删除操作', () => {
    const permissions = new Set([
      'approval:create',
      'asset:edit',
      'asset:view',
    ]);
    const access = buildAssetRowActionAccess((codes) =>
      codes.some((code) => permissions.has(code)),
    );

    expect(access.canDelete).toBe(false);
    expect(access.canEdit).toBe(true);
    expect(access.canBorrow).toBe(true);
    expect(access.canCreate).toBe(false);
  });

  it('授予 asset:delete 后允许显示删除操作', () => {
    const permissions = new Set(['asset:delete']);
    const access = buildAssetRowActionAccess((codes) =>
      codes.some((code) => permissions.has(code)),
    );

    expect(access.canDelete).toBe(true);
  });

  it('顶部导入导出和新增资产使用独立权限码', () => {
    const permissions = new Set(['asset:create', 'asset:export']);
    const access = buildAssetRowActionAccess((codes) =>
      codes.some((code) => permissions.has(code)),
    );

    expect(access.canCreate).toBe(true);
    expect(access.canExport).toBe(true);
    expect(access.canImport).toBe(false);
  });
});

describe('资产状态操作', () => {
  it('只有未删除的在库资产允许执行在库操作', () => {
    expect(canRunAvailableAssetAction({ isDeleted: false, status: 0 })).toBe(
      true,
    );
    expect(canRunAvailableAssetAction({ isDeleted: false, status: 1 })).toBe(
      false,
    );
    expect(canRunAvailableAssetAction({ isDeleted: true, status: 0 })).toBe(
      false,
    );
  });

  it('当前保管人不能借用自己保管的在库资产', () => {
    expect(
      canBorrowAvailableAsset(
        { custodianId: 10, isDeleted: false, status: 0 },
        10,
      ),
    ).toBe(false);
    expect(
      canBorrowAvailableAsset(
        { custodianId: 10, isDeleted: false, status: 0 },
        11,
      ),
    ).toBe(true);
    expect(
      canBorrowAvailableAsset(
        { custodianId: null, isDeleted: false, status: 0 },
        10,
      ),
    ).toBe(true);
  });

  it('当前保管人可以转让未删除的在库或借用中资产', () => {
    expect(
      canTransferAvailableAsset(
        { custodianId: 10, isDeleted: false, status: 0 },
        10,
      ),
    ).toBe(true);
    expect(
      canTransferAvailableAsset(
        { custodianId: 10, isDeleted: false, status: 0 },
        11,
      ),
    ).toBe(false);
    expect(
      canTransferAvailableAsset(
        { custodianId: null, isDeleted: false, status: 0 },
        10,
      ),
    ).toBe(false);
    expect(
      canTransferAvailableAsset(
        { custodianId: 10, isDeleted: false, status: 1 },
        10,
      ),
    ).toBe(true);
  });
});
