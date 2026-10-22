import type {
  ProjectDocumentDto,
  ProjectStageDto,
  ProjectWorkspaceDto,
  SaveProjectStagePayload,
} from '#/api/project-workspace';

import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createProjectStageApi,
  deleteProjectDocumentApi,
  deleteProjectStageApi,
  downloadProjectDocumentApi,
  downloadProjectPackageApi,
  downloadProjectReportApi,
  getProjectWorkspaceApi,
  updateProjectStageApi,
  uploadProjectDocumentApi,
} from '#/api/project-workspace';
import { downloadBlob } from '#/utils/download';

import {
  createEmptyStageForm,
  isProjectTesting,
  stageToForm,
  validateProjectStageForm,
} from './project-testing-rules';
import { createWorkspaceRequestCoordinator } from './workspace-request-coordinator';

export interface ProjectTestingWorkspaceProps {
  enabled: boolean;
  projectClosedDate?: null | string;
  projectId: number;
  projectProgressCode?: null | string;
}

function errorText(error: unknown) {
  return error instanceof Error && error.message.trim()
    ? error.message
    : '测试阶段与报告加载失败';
}

function optionalText(value: string): null | string {
  return value.trim() ? value : null;
}

function safeFileName(value: string) {
  return value.replaceAll(/[\\/:*?"<>|]/g, '_');
}

export function useProjectTestingWorkspace(
  props: Readonly<ProjectTestingWorkspaceProps>,
) {
  const requestCoordinator = createWorkspaceRequestCoordinator();
  let operationGeneration = 0;
  const workspace = ref<null | ProjectWorkspaceDto>(null);
  const loading = ref(false);
  const loadError = ref('');
  const stageDialogVisible = ref(false);
  const editingStage = ref<null | ProjectStageDto>(null);
  const savingStage = ref(false);
  const deletingStageId = ref<null | number>(null);
  const uploadingDocument = ref(false);
  const uploadingStageId = ref<null | number | undefined>();
  const deletingDocumentId = ref<null | number>(null);
  const downloadingKey = ref('');
  const stageForm = reactive(createEmptyStageForm());

  const canManage = computed(() => workspace.value?.canManage === true);
  const canCreateStage = computed(
    () => canManage.value && isProjectTesting(props.projectProgressCode),
  );
  const canExport = computed(() => workspace.value?.canExport === true);
  const writeBusy = computed(
    () =>
      savingStage.value ||
      deletingStageId.value !== null ||
      uploadingDocument.value ||
      deletingDocumentId.value !== null,
  );

  function isCurrentOperation(projectId: number, generation: number) {
    return (
      generation === operationGeneration &&
      props.enabled &&
      props.projectId === projectId
    );
  }

  function resetViewState() {
    workspace.value = null;
    loading.value = false;
    loadError.value = '';
    stageDialogVisible.value = false;
    editingStage.value = null;
    savingStage.value = false;
    deletingStageId.value = null;
    uploadingDocument.value = false;
    uploadingStageId.value = undefined;
    deletingDocumentId.value = null;
    downloadingKey.value = '';
  }

  async function loadWorkspace() {
    const projectId = props.projectId;
    await requestCoordinator.load({
      enabled: props.enabled,
      onError(error) {
        workspace.value = null;
        loadError.value = errorText(error);
      },
      onSettled() {
        loading.value = false;
      },
      onStart() {
        if (workspace.value?.projectId !== projectId) workspace.value = null;
        loadError.value = '';
        loading.value = true;
      },
      onSuccess(value) {
        workspace.value = value;
      },
      projectId,
      request: getProjectWorkspaceApi,
    });
  }

  function openCreateStage() {
    if (!canCreateStage.value || writeBusy.value) return;
    const nextSequence = Math.min(
      999,
      Math.max(
        0,
        ...(workspace.value?.stages.map((item) => item.sequence) ?? []),
      ) + 1,
    );
    Object.assign(stageForm, createEmptyStageForm(nextSequence));
    editingStage.value = null;
    stageDialogVisible.value = true;
  }

  function openEditStage(stage: ProjectStageDto) {
    if (!canManage.value || writeBusy.value) return;
    Object.assign(stageForm, stageToForm(stage));
    editingStage.value = stage;
    stageDialogVisible.value = true;
  }

  function buildStagePayload(): SaveProjectStagePayload {
    return {
      endDate: stageForm.endDate || null,
      name: stageForm.name.trim(),
      objective: optionalText(stageForm.objective),
      rowVersion: stageForm.rowVersion,
      sequence: stageForm.sequence ?? 0,
      startDate: stageForm.startDate || null,
      status: stageForm.status,
      testContent: optionalText(stageForm.testContent),
      testResult: optionalText(stageForm.testResult),
    };
  }

  async function saveStage() {
    if (
      (!editingStage.value ? !canCreateStage.value : !canManage.value) ||
      writeBusy.value
    )
      return;
    const validationError = validateProjectStageForm(stageForm);
    if (validationError) {
      ElMessage.warning(validationError);
      return;
    }
    const projectId = props.projectId;
    const generation = operationGeneration;
    const currentEditingStage = editingStage.value;
    if (currentEditingStage && currentEditingStage.projectId !== projectId)
      return;
    savingStage.value = true;
    try {
      await (currentEditingStage
        ? updateProjectStageApi(
            projectId,
            currentEditingStage.id,
            buildStagePayload(),
          )
        : createProjectStageApi(projectId, buildStagePayload()));
      if (!isCurrentOperation(projectId, generation)) return;
      ElMessage.success(currentEditingStage ? '阶段已更新' : '阶段已新增');
      stageDialogVisible.value = false;
      await loadWorkspace();
    } catch {
      // 请求拦截器负责展示后端错误；保留弹窗和完整草稿供修正或重试。
    } finally {
      if (isCurrentOperation(projectId, generation)) {
        savingStage.value = false;
      }
    }
  }

  async function removeStage(stage: ProjectStageDto) {
    if (!canManage.value || writeBusy.value) return;
    const projectId = props.projectId;
    const generation = operationGeneration;
    if (stage.projectId !== projectId) return;
    const linkedDocumentCount =
      workspace.value?.documents.filter((item) => item.stageId === stage.id)
        .length ?? 0;
    if (linkedDocumentCount > 0) {
      ElMessage.warning(
        `该阶段已有 ${linkedDocumentCount} 份关联报告，需先处理关联报告后才能删除`,
      );
      return;
    }
    deletingStageId.value = stage.id;
    try {
      await ElMessageBox.confirm(
        `确认删除阶段“${stage.name}”？已有报告的阶段需先处理关联报告后才能删除。`,
        '删除阶段',
        {
          cancelButtonText: '取消',
          confirmButtonText: '确认删除',
          type: 'warning',
        },
      );
      if (!isCurrentOperation(projectId, generation)) return;
      await deleteProjectStageApi(projectId, stage.id, stage.rowVersion);
      if (!isCurrentOperation(projectId, generation)) return;
      ElMessage.success('阶段已删除');
      await loadWorkspace();
    } catch {
      // 错误由请求拦截器展示。
    } finally {
      if (isCurrentOperation(projectId, generation)) {
        deletingStageId.value = null;
      }
    }
  }

  async function uploadDocument(
    file: File,
    stageId: number,
    description: string,
    onSuccess?: () => void,
  ) {
    if (!canManage.value || writeBusy.value) return;
    const projectId = props.projectId;
    const generation = operationGeneration;
    if (!workspace.value?.stages.some((stage) => stage.id === stageId)) {
      ElMessage.warning('所选阶段已变更，请重新选择报告归属');
      return;
    }
    uploadingDocument.value = true;
    uploadingStageId.value = stageId;
    try {
      await uploadProjectDocumentApi(
        projectId,
        file,
        stageId,
        optionalText(description),
      );
      if (!isCurrentOperation(projectId, generation)) return;
      ElMessage.success('报告已上传');
      onSuccess?.();
      await loadWorkspace();
    } catch {
      // 上传失败时保留已选文件和说明，便于直接重试。
    } finally {
      if (isCurrentOperation(projectId, generation)) {
        uploadingDocument.value = false;
        uploadingStageId.value = undefined;
      }
    }
  }

  async function removeDocument(document: ProjectDocumentDto) {
    if (!canManage.value || writeBusy.value) return;
    const projectId = props.projectId;
    const generation = operationGeneration;
    if (document.projectId !== projectId) return;
    deletingDocumentId.value = document.id;
    try {
      await ElMessageBox.confirm(
        `确认删除报告“${document.fileName}”？`,
        '删除报告',
        {
          cancelButtonText: '取消',
          confirmButtonText: '确认删除',
          type: 'warning',
        },
      );
      if (!isCurrentOperation(projectId, generation)) return;
      await deleteProjectDocumentApi(projectId, document.id);
      if (!isCurrentOperation(projectId, generation)) return;
      ElMessage.success('报告已删除');
      await loadWorkspace();
    } catch {
      // 错误由请求拦截器展示。
    } finally {
      if (isCurrentOperation(projectId, generation)) {
        deletingDocumentId.value = null;
      }
    }
  }

  async function runDownload(
    key: string,
    projectId: number,
    request: () => Promise<Blob>,
    name: string,
  ) {
    if (downloadingKey.value) return;
    const generation = operationGeneration;
    downloadingKey.value = key;
    try {
      const blob = await request();
      if (!isCurrentOperation(projectId, generation)) return;
      downloadBlob(blob, safeFileName(name));
    } catch {
      // 错误由请求拦截器展示。
    } finally {
      if (isCurrentOperation(projectId, generation)) {
        downloadingKey.value = '';
      }
    }
  }

  function downloadDocument(document: ProjectDocumentDto) {
    if (!canExport.value) return;
    const projectId = props.projectId;
    if (document.projectId !== projectId) return;
    return runDownload(
      `document-${document.id}`,
      projectId,
      async () => {
        const response = await downloadProjectDocumentApi(
          projectId,
          document.id,
        );
        return response.data;
      },
      document.fileName,
    );
  }

  function downloadReport(stage?: ProjectStageDto) {
    if (!canExport.value) return;
    const projectId = props.projectId;
    if (stage && stage.projectId !== projectId) return;
    return runDownload(
      stage ? `report-${stage.id}` : 'report-all',
      projectId,
      async () => {
        const response = await downloadProjectReportApi(projectId, stage?.id);
        return response.data;
      },
      stage
        ? `测试阶段_${stage.sequence}_${stage.name}.docx`
        : `项目_${projectId}_测试报告.docx`,
    );
  }

  function downloadPackage(stage?: ProjectStageDto) {
    if (!canExport.value) return;
    const projectId = props.projectId;
    if (stage && stage.projectId !== projectId) return;
    return runDownload(
      stage ? `package-${stage.id}` : 'package-all',
      projectId,
      async () => {
        const response = await downloadProjectPackageApi(projectId, stage?.id);
        return response.data;
      },
      stage
        ? `测试阶段_${stage.sequence}_${stage.name}_资料包.zip`
        : `项目_${projectId}_全部资料包.zip`,
    );
  }

  watch(
    () => [props.enabled, props.projectId] as const,
    ([enabled]) => {
      operationGeneration += 1;
      requestCoordinator.invalidate();
      if (!enabled || props.projectId <= 0) {
        resetViewState();
        return;
      }
      resetViewState();
      void loadWorkspace();
    },
    { immediate: true },
  );

  onBeforeUnmount(() => {
    operationGeneration += 1;
    requestCoordinator.invalidate();
  });

  return {
    canCreateStage,
    canExport,
    canManage,
    deletingDocumentId,
    deletingStageId,
    downloadDocument,
    downloadingKey,
    downloadPackage,
    downloadReport,
    editingStage,
    loadError,
    loading,
    loadWorkspace,
    openCreateStage,
    openEditStage,
    removeDocument,
    removeStage,
    saveStage,
    savingStage,
    stageDialogVisible,
    stageForm,
    uploadDocument,
    uploadingDocument,
    uploadingStageId,
    workspace,
    writeBusy,
  };
}
