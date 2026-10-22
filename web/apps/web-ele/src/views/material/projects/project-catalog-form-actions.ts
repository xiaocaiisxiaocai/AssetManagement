import type {
  SaveTestProjectPayload,
  TestProjectItem,
} from '#/api/test-project';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createTestProjectApi,
  updateTestProjectApi,
  updateTestProjectProgressApi,
} from '#/api/test-project';
import { runHandled } from '#/utils/handled-promise';
import { mergeSelectedUserOption } from '#/utils/user-options';

import { validateProjectForm } from './project-form-rules';

export function bindProjectCatalogFormActions(
  page: Record<string, any>,
  deps: {
    loadBaseOptions: () => Promise<void>;
    loadData: () => Promise<void>;
    loadUsers: () => Promise<void>;
  },
) {
  const { loadBaseOptions, loadData, loadUsers } = deps;
  const {
    users,
    dialogVisible,
    editingId,
    saving,
    form,
    currentUserId,
    progressDialogVisible,
    progressEditingProject,
    progressSaving,
    progressForm,
    progressBaseline,
    normalizeText,
  } = page;

  async function confirmClosing(nextProgress: string, alreadyClosed: boolean) {
    if (nextProgress !== 'closed' || alreadyClosed) return true;
    try {
      await ElMessageBox.confirm(
        '结案后项目不能再修改，只有系统管理员可以重新打开。确认结案？',
        '确认结案',
        {
          cancelButtonText: '取消',
          confirmButtonText: '确认结案',
          type: 'warning',
        },
      );
      return true;
    } catch {
      return false;
    }
  }

  function resetProjectForm() {
    Object.assign(form, {
      closedDate: '',
      code: '',
      followUpIntervalDays: 14,
      name: '',
      ownerId: currentUserId.value || undefined,
      plannedFinishDate: '',
      progressCode: 'planning',
      projectTypeCode: '',
      startDate: '',
      testStatus: '',
      rowVersion: 0,
    });
  }

  function openCreate() {
    editingId.value = null;
    progressBaseline.value = '';
    resetProjectForm();
    dialogVisible.value = true;
    runHandled(Promise.all([loadUsers(), loadBaseOptions()]));
  }

  function openEdit(row: TestProjectItem) {
    if (row.closedDate) {
      ElMessage.warning('项目已结案，不能再修改');
      return;
    }
    users.value = mergeSelectedUserOption(users.value, {
      id: row.ownerId,
      name: row.ownerName,
    });
    editingId.value = row.id;
    progressBaseline.value = (row.progressCode ?? '').toLowerCase();
    Object.assign(form, {
      closedDate: row.closedDate ? row.closedDate.slice(0, 10) : '',
      code: row.code ?? '',
      followUpIntervalDays: row.followUpIntervalDays || 14,
      name: row.name,
      ownerId: row.ownerId ?? undefined,
      plannedFinishDate: row.plannedFinishDate
        ? row.plannedFinishDate.slice(0, 10)
        : '',
      progressCode: row.progressCode ?? '',
      projectTypeCode: row.projectTypeCode ?? '',
      startDate: row.startDate ? row.startDate.slice(0, 10) : '',
      testStatus: row.testStatus ?? '',
      rowVersion: row.rowVersion ?? 0,
    });
    dialogVisible.value = true;
    runHandled(Promise.all([loadUsers(), loadBaseOptions()]));
  }

  function openProgress(row: TestProjectItem) {
    if (row.closedDate) {
      ElMessage.warning('项目已结案，不能再修改');
      return;
    }
    progressEditingProject.value = row;
    progressBaseline.value = (row.progressCode ?? '').toLowerCase();
    Object.assign(progressForm, {
      closedDate: row.closedDate ? row.closedDate.slice(0, 10) : '',
      progressCode: row.progressCode ?? '',
      testStatus: row.testStatus ?? '',
      rowVersion: row.rowVersion ?? 0,
    });
    progressDialogVisible.value = true;
  }

  function buildProjectPayload(): SaveTestProjectPayload {
    return {
      closedDate: form.closedDate || null,
      code: normalizeText(form.code),
      followUpIntervalDays: form.followUpIntervalDays || 14,
      name: form.name.trim(),
      ownerId: form.ownerId ?? null,
      plannedFinishDate: form.plannedFinishDate || null,
      progressCode: form.progressCode || null,
      projectTypeCode: form.projectTypeCode || null,
      startDate: form.startDate || null,
      testStatus: normalizeText(form.testStatus),
      rowVersion: form.rowVersion,
    };
  }

  async function save() {
    const validationMessage = validateProjectForm(form);
    if (validationMessage) {
      ElMessage.warning(validationMessage);
      return;
    }
    if (
      !(await confirmClosing(
        form.progressCode,
        progressBaseline.value === 'closed',
      ))
    )
      return;
    if (saving.value) return;
    saving.value = true;
    try {
      const payload = buildProjectPayload();
      await (editingId.value
        ? updateTestProjectApi(editingId.value, payload)
        : createTestProjectApi(payload));
      ElMessage.success('保存成功');
      dialogVisible.value = false;
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      saving.value = false;
    }
  }

  async function saveProgress() {
    const project = progressEditingProject.value;
    if (!project) return;
    if (project.closedDate) {
      ElMessage.warning('项目已结案，不能再修改');
      progressDialogVisible.value = false;
      return;
    }
    if (!progressForm.progressCode) {
      ElMessage.warning('请选择项目进度');
      return;
    }
    if (progressForm.progressCode === 'closed' && !progressForm.closedDate) {
      ElMessage.warning('已结案项目必须填写结案时间');
      return;
    }
    if (
      progressForm.closedDate &&
      project.startDate &&
      progressForm.closedDate < project.startDate.slice(0, 10)
    ) {
      ElMessage.warning('结案时间不能早于开始时间');
      return;
    }
    if (
      !(await confirmClosing(
        progressForm.progressCode,
        (project.progressCode ?? '').toLowerCase() === 'closed',
      ))
    )
      return;
    if (progressSaving.value) return;
    progressSaving.value = true;
    try {
      await updateTestProjectProgressApi(project.id, {
        closedDate: progressForm.closedDate || null,
        progressCode: progressForm.progressCode,
        rowVersion: progressForm.rowVersion,
        testStatus: normalizeText(progressForm.testStatus),
      });
      ElMessage.success('项目进展已更新');
      progressDialogVisible.value = false;
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      progressSaving.value = false;
    }
  }

  return {
    resetProjectForm,
    openCreate,
    openEdit,
    openProgress,
    buildProjectPayload,
    save,
    saveProgress,
  };
}
