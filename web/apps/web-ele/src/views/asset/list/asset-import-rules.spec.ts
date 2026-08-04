import { describe, expect, it } from 'vitest';

import {
  canConfirmAssetImport,
  summarizeAssetImportRows,
} from './asset-import-rules';

describe('资产批量导入规则', () => {
  const rows = [
    { error: '', isValid: true },
    { error: '分类编码不存在', isValid: false },
    { error: '名称必填；分类编码不存在', isValid: false },
  ];

  it('分类不存在的行不计入可导入数量', () => {
    expect(summarizeAssetImportRows(rows)).toEqual({
      invalidCount: 2,
      missingCategoryCount: 2,
      totalCount: 3,
      validCount: 1,
    });
  });

  it('存在有效行时允许跳过无效分类并确认导入', () => {
    expect(canConfirmAssetImport(false, true, rows)).toBe(true);
    expect(canConfirmAssetImport(true, true, rows)).toBe(false);
    expect(canConfirmAssetImport(false, false, rows)).toBe(false);
    expect(
      canConfirmAssetImport(false, true, [
        { error: '分类编码不存在', isValid: false },
      ]),
    ).toBe(false);
  });
});
