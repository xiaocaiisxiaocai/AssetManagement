import { ElMessage } from 'element-plus';

import {
  downloadUserImportTemplateApi,
  importUsersApi,
  validateUserImportApi,
} from '#/api/user';

export function bindUserImportActions(page: Record<string, any>) {
  const {
    importDialogVisible,
    importValidation,
    importing,
    importRows,
    selectedImportFile,
    query,
    loadData,
  } = page;

  function downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }

  async function downloadImportTemplate() {
    const response = await downloadUserImportTemplateApi();
    downloadBlob(response.data, 'user-import-template.xlsx');
  }

  function openImport() {
    importValidation.reset();
    importDialogVisible.value = true;
  }

  async function onImportFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const requestGeneration = importValidation.start(input.files?.[0] ?? null);
    if (requestGeneration === null || !selectedImportFile.value) return;

    const file = selectedImportFile.value;
    try {
      const result = await validateUserImportApi(file);
      if (!importValidation.canApply(requestGeneration)) return;
      importRows.value = result.rows;
      if (result.failedCount > 0) {
        ElMessage.warning(
          `预览发现 ${result.failedCount} 条错误，请修正后重新选择文件`,
        );
      } else {
        ElMessage.success(`预览通过 ${result.successCount} 条`);
      }
    } finally {
      importValidation.finish(requestGeneration);
    }
  }

  async function confirmImportUsers() {
    if (!selectedImportFile.value) {
      ElMessage.warning('请先选择 Excel 文件');
      return;
    }

    importing.value = true;
    try {
      const result = await importUsersApi(selectedImportFile.value);
      importRows.value = result.rows;
      if (result.failedCount > 0) {
        ElMessage.warning(
          `导入失败 ${result.failedCount} 条，请修正后重新导入`,
        );
        return;
      }
      ElMessage.success(`导入成功 ${result.successCount} 条`);
      importDialogVisible.value = false;
      query.page = 1;
      await loadData();
    } finally {
      importing.value = false;
    }
  }

  return {
    downloadBlob,
    downloadImportTemplate,
    openImport,
    onImportFileChange,
    confirmImportUsers,
  };
}
