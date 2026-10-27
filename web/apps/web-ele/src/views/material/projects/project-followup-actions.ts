import type {
  SaveTestProjectFollowupPayload,
  TestProjectFollowup,
  TestProjectItem,
} from '#/api/test-project';

import { ElMessage } from 'element-plus';

import {
  createTestProjectFollowupApi,
  listTestProjectFollowupsApi,
  updateTestProjectFollowupApi,
} from '#/api/test-project';

export function bindProjectFollowupActions(
  page: Record<string, any>,
  deps: {
    loadProjectFlows: (
      projectId?: number,
      kind?: 'handled' | 'mine' | 'pending',
    ) => Promise<void>;
    loadProjectMaterials: (projectId?: number) => Promise<void>;
    resetMaterialQuery: () => void;
  },
) {
  const {
    currentProject,
    followupDrawerVisible,
    followups,
    followupLoading,
    followupSaving,
    followupOperationGuard,
    editingFollowupId,
    followupForm,
    followupRequestGuard,
    materialRequestGuard,
    materials,
    materialTotal,
    pendingFlowRequestGuard,
    handledFlowRequestGuard,
    myFlowRequestGuard,
    pendingFlows,
    handledFlows,
    myFlows,
    pendingFlowTotal,
    handledFlowTotal,
    myFlowTotal,
    pendingFlowQuery,
    myFlowQuery,
    handledFlowQuery,
    loadData,
  } = page;

  async function openFollowups(row: TestProjectItem) {
    if (followupSaving.value) {
      ElMessage.warning('跟进正在保存，请稍候');
      return;
    }
    followupOperationGuard.invalidate();
    followupRequestGuard.invalidate();
    materialRequestGuard.invalidate();
    pendingFlowRequestGuard.invalidate();
    handledFlowRequestGuard.invalidate();
    myFlowRequestGuard.invalidate();
    currentProject.value = row;
    materials.value = [];
    materialTotal.value = 0;
    pendingFlows.value = [];
    handledFlows.value = [];
    myFlows.value = [];
    pendingFlowTotal.value = 0;
    handledFlowTotal.value = 0;
    myFlowTotal.value = 0;
    pendingFlowQuery.page = 1;
    handledFlowQuery.page = 1;
    myFlowQuery.page = 1;
    deps.resetMaterialQuery();
    followups.value = [];
    editingFollowupId.value = null;
    Object.assign(followupForm, { content: '' });
    followupDrawerVisible.value = true;
    await Promise.allSettled([
      loadFollowups(row.id),
      deps.loadProjectMaterials(row.id),
      deps.loadProjectFlows(row.id),
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
    Object.assign(followupForm, { content: row.content });
  }

  function cancelFollowupEdit() {
    editingFollowupId.value = null;
    Object.assign(followupForm, { content: '' });
  }

  async function saveFollowup() {
    const project = currentProject.value;
    if (!project) return;
    if (project.testingIncomplete) {
      ElMessage.warning(
        '测试阶段仍在进行或尚未填写结束日期，完成后再填写落地跟进',
      );
      return;
    }
    if (project.isDeleted || !project.canWriteFollowUp) {
      ElMessage.warning('项目进入落地跟进后，负责人或管理员才能填写');
      return;
    }
    if (!followupForm.content.trim()) {
      ElMessage.warning('请填写落地情况');
      return;
    }
    if (followupSaving.value) return;
    const operationGeneration = followupOperationGuard.next();
    const projectId = project.id;
    const editingId = editingFollowupId.value;
    followupSaving.value = true;
    try {
      const payload: SaveTestProjectFollowupPayload = {
        content: followupForm.content.trim(),
      };
      await (editingId
        ? updateTestProjectFollowupApi(projectId, editingId, payload)
        : createTestProjectFollowupApi(projectId, payload));
      if (
        !followupOperationGuard.isLatest(operationGeneration) ||
        !followupDrawerVisible.value ||
        currentProject.value?.id !== projectId
      )
        return;
      ElMessage.success('跟进已保存');
      await Promise.all([loadFollowups(projectId), loadData()]);
      if (
        followupOperationGuard.isLatest(operationGeneration) &&
        followupDrawerVisible.value &&
        currentProject.value?.id === projectId
      )
        cancelFollowupEdit();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      if (followupOperationGuard.isLatest(operationGeneration))
        followupSaving.value = false;
    }
  }

  return {
    openFollowups,
    loadFollowups,
    editFollowup,
    cancelFollowupEdit,
    saveFollowup,
  };
}
