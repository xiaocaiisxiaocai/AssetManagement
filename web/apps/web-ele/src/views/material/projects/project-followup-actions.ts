import type { TestProjectFollowup, TestProjectItem } from '#/api/test-project';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createTestProjectFollowupApi,
  deleteTestProjectFollowupApi,
  listTestProjectFollowupsApi,
  updateTestProjectFollowupApi,
} from '#/api/test-project';

import { isFutureFollowupDate, localTodayText } from './followup-date-rules';

export function bindProjectFollowupActions(
  page: Record<string, any>,
  deps: {
    loadProjectMaterials: (projectId?: number) => Promise<void>;
    resetMaterialQuery: () => void;
  },
) {
  const {
    currentProject,
    currentMaterialActionAccess,
    followupDrawerVisible,
    activeProjectTab,
    followups,
    followupLoading,
    followupSaving,
    editingFollowupId,
    followupForm,
    followupRequestGuard,
    materialRequestGuard,
    pendingFlowRequestGuard,
    handledFlowRequestGuard,
    myFlowRequestGuard,
    pendingFlows,
    handledFlows,
    myFlows,
    flowActiveTab,
    pendingFlowQuery,
    myFlowQuery,
    handledFlowQuery,
    loadData,
  } = page;

  async function openFollowups(row: TestProjectItem) {
    followupRequestGuard.invalidate();
    materialRequestGuard.invalidate();
    pendingFlowRequestGuard.invalidate();
    handledFlowRequestGuard.invalidate();
    myFlowRequestGuard.invalidate();
    currentProject.value = row;
    pendingFlows.value = [];
    handledFlows.value = [];
    myFlows.value = [];
    flowActiveTab.value = currentMaterialActionAccess.value.canApprove
      ? 'pending'
      : 'mine';
    pendingFlowQuery.page = 1;
    handledFlowQuery.page = 1;
    myFlowQuery.page = 1;
    deps.resetMaterialQuery();
    activeProjectTab.value = 'materials';
    editingFollowupId.value = null;
    Object.assign(followupForm, {
      content: '',
      dueDate: localTodayText(),
    });
    followupDrawerVisible.value = true;
    await Promise.allSettled([
      loadFollowups(row.id),
      deps.loadProjectMaterials(row.id),
    ]);
  }

  async function loadFollowups(projectId: number) {
    const requestGeneration = followupRequestGuard.next();
    followupLoading.value = true;
    try {
      const response = await listTestProjectFollowupsApi(projectId);
      if (
        followupRequestGuard.isLatest(requestGeneration) &&
        followupDrawerVisible.value &&
        currentProject.value?.id === projectId
      ) {
        followups.value = response;
      }
    } finally {
      if (followupRequestGuard.isLatest(requestGeneration)) {
        followupLoading.value = false;
      }
    }
  }

  function editFollowup(row: TestProjectFollowup) {
    editingFollowupId.value = row.id;
    Object.assign(followupForm, {
      content: row.content,
      dueDate: row.dueDate ? row.dueDate.slice(0, 10) : '',
    });
  }

  function cancelFollowupEdit() {
    editingFollowupId.value = null;
    Object.assign(followupForm, {
      content: '',
      dueDate: localTodayText(),
    });
  }

  async function saveFollowup() {
    const project = currentProject.value;
    if (!project) return;
    if (project.isDeleted || !project.canWriteFollowUp) {
      ElMessage.warning('项目进入落地跟进后，负责人或管理员才能填写');
      return;
    }
    if (!followupForm.content.trim()) {
      ElMessage.warning('请填写落地情况');
      return;
    }
    if (
      followupForm.dueDate &&
      isFutureFollowupDate(new Date(`${followupForm.dueDate}T00:00:00`))
    ) {
      ElMessage.warning('跟进日期不能晚于今天');
      return;
    }
    followupSaving.value = true;
    try {
      const payload = {
        content: followupForm.content.trim(),
        dueDate: followupForm.dueDate || null,
      };
      await (editingFollowupId.value
        ? updateTestProjectFollowupApi(
            project.id,
            editingFollowupId.value,
            payload,
          )
        : createTestProjectFollowupApi(project.id, payload));
      ElMessage.success('跟进已保存');
      await Promise.all([loadFollowups(project.id), loadData()]);
      cancelFollowupEdit();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      followupSaving.value = false;
    }
  }

  async function deleteFollowup(row: TestProjectFollowup) {
    const project = currentProject.value;
    if (!project) return;
    if (project.isDeleted || !project.canWriteFollowUp) {
      ElMessage.warning('当前项目为只读状态');
      return;
    }
    try {
      await ElMessageBox.confirm('确认删除该跟进记录？', '删除确认', {
        type: 'warning',
      });
    } catch {
      return;
    }
    try {
      await deleteTestProjectFollowupApi(project.id, row.id);
      ElMessage.success('已删除');
      await Promise.all([loadFollowups(project.id), loadData()]);
      if (editingFollowupId.value === row.id) cancelFollowupEdit();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  return {
    openFollowups,
    loadFollowups,
    editFollowup,
    cancelFollowupEdit,
    saveFollowup,
    deleteFollowup,
  };
}
