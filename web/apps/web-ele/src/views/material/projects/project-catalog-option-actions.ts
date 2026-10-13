import type { OptionKind } from './project-workspace-types';

import type {
  SaveTestProjectOptionPayload,
  TestProjectOption,
} from '#/api/test-project';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createTestProjectOptionApi,
  deleteTestProjectOptionApi,
  updateTestProjectOptionApi,
} from '#/api/test-project';

export function bindProjectCatalogOptionActions(
  page: Record<string, any>,
  deps: {
    loadData: () => Promise<void>;
    loadOptions: () => Promise<void>;
  },
) {
  const { loadData, loadOptions } = deps;
  const {
    optionDialogVisible,
    optionSaving,
    optionEditingId,
    activeOptionKind,
    optionForm,
    generateOptionCode,
  } = page;

  function resetOptionForm(kind: OptionKind = activeOptionKind.value) {
    Object.assign(optionForm, {
      code: '',
      isActive: true,
      kind,
      label: '',
      sort: 0,
    });
    optionEditingId.value = null;
  }

  function openOptionDialog() {
    resetOptionForm();
    optionDialogVisible.value = true;
  }

  function openOptionCreate(kind: OptionKind) {
    activeOptionKind.value = kind;
    resetOptionForm(kind);
  }

  function openOptionEdit(row: TestProjectOption) {
    activeOptionKind.value = row.kind;
    optionEditingId.value = row.id;
    Object.assign(optionForm, {
      code: row.code,
      isActive: row.isActive,
      kind: row.kind,
      label: row.label,
      sort: row.sort,
    });
  }

  async function saveOption() {
    if (!optionForm.label.trim()) {
      ElMessage.warning('请填写名称');
      return;
    }
    optionSaving.value = true;
    try {
      const payload: SaveTestProjectOptionPayload = {
        code: optionForm.code.trim() || generateOptionCode(optionForm.kind),
        isActive: optionForm.isActive,
        kind: optionForm.kind,
        label: optionForm.label.trim(),
        sort: optionForm.sort || 0,
      };
      await (optionEditingId.value
        ? updateTestProjectOptionApi(optionEditingId.value, payload)
        : createTestProjectOptionApi(payload));
      ElMessage.success('配置已保存');
      resetOptionForm(optionForm.kind);
      await loadOptions();
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      optionSaving.value = false;
    }
  }

  async function removeOption(row: TestProjectOption) {
    try {
      await ElMessageBox.confirm(
        `确认删除配置「${row.label}」？正在使用的配置不能删除。`,
        '删除确认',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await deleteTestProjectOptionApi(row.id);
      ElMessage.success('配置已删除');
      if (optionEditingId.value === row.id) resetOptionForm(row.kind);
      await loadOptions();
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  return {
    resetOptionForm,
    openOptionDialog,
    openOptionCreate,
    openOptionEdit,
    saveOption,
    removeOption,
  };
}
