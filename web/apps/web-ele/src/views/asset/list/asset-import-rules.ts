type AssetImportRowState = {
  error: string;
  isValid: boolean;
};

export function summarizeAssetImportRows(rows: AssetImportRowState[]) {
  const validCount = rows.filter((row) => row.isValid).length;
  return {
    invalidCount: rows.length - validCount,
    missingCategoryCount: rows.filter(
      (row) => !row.isValid && row.error.includes('分类编码不存在'),
    ).length,
    totalCount: rows.length,
    validCount,
  };
}

export function canConfirmAssetImport(
  loading: boolean,
  hasSelectedFile: boolean,
  rows: AssetImportRowState[],
) {
  return !loading && hasSelectedFile && rows.some((row) => row.isValid);
}
