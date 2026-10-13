import { computed, defineAsyncComponent, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createWorkflowApi,
  deleteWorkflowApi,
  getWorkflowsApi,
  saveWorkflowApi,
  saveWorkflowDesignApi,
  type SaveWorkflowPayload,
  setWorkflowStatusApi,
  type WorkflowItem,
} from '#/api/workflow';
import { createSingleFlight } from '#/utils/single-flight';
import { buildWorkflowActionAccess } from '#/views/permissions/action-access';

import { isWorkflowDesignerBusy } from './designer-save-state';

export function useWorkflowAdmin() {
  const BpmnModeler = defineAsyncComponent(() => import('./bpmn-modeler.vue'));

  const { hasAccessByCodes } = useAccess();
  const workflowActionAccess = computed(() =>
    buildWorkflowActionAccess(hasAccessByCodes),
  );
  const loading = ref(false);
  const workflows = ref<WorkflowItem[]>([]);
  const dialogVisible = ref(false);
  const currentWorkflow = ref<null | WorkflowItem>(null);
  const formDialogVisible = ref(false);
  const formSaving = ref(false);
  const designSaving = ref(false);
  const designerExporting = ref(false);
  const designerBusy = computed(() =>
    isWorkflowDesignerBusy(designerExporting.value, designSaving.value),
  );
  const editingWorkflow = ref<null | WorkflowItem>(null);
  const workflowForm = ref<SaveWorkflowPayload>({
    bizType: '',
    bpmnXml: null,
    name: '',
  });

  const bizTypeOptions = [
    { label: '资产借用', value: 'borrow' },
    { label: '借用延期', value: 'extension' },
    { label: '资产转让', value: 'transfer' },
    { label: '资产归还', value: 'return' },
    { label: '测试料件流转', value: 'material_transfer' },
  ];

  const loadWorkflows = async () => {
    loading.value = true;
    try {
      workflows.value = await getWorkflowsApi();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      loading.value = false;
    }
  };

  const openDesigner = (workflow: WorkflowItem) => {
    currentWorkflow.value = workflow;
    designerExporting.value = false;
    dialogVisible.value = true;
  };

  const openCreateDialog = () => {
    editingWorkflow.value = null;
    workflowForm.value = {
      bizType: '',
      bpmnXml: null,
      name: '',
    };
    formDialogVisible.value = true;
  };

  const openEditDialog = (workflow: WorkflowItem) => {
    editingWorkflow.value = workflow;
    workflowForm.value = {
      bizType: workflow.bizType,
      bpmnXml: workflow.bpmnXml || null,
      name: workflow.name,
    };
    formDialogVisible.value = true;
  };

  const handleFormSave = async () => {
    const name = workflowForm.value.name.trim();
    const bizType = workflowForm.value.bizType.trim();
    if (!name) {
      ElMessage.warning('请填写流程名称');
      return;
    }
    if (!bizType) {
      ElMessage.warning('请填写业务类型');
      return;
    }

    formSaving.value = true;
    try {
      const payload = {
        ...workflowForm.value,
        bizType,
        name,
      };

      if (editingWorkflow.value) {
        await saveWorkflowApi(editingWorkflow.value.id, payload);
        ElMessage.success('修改成功');
      } else {
        await createWorkflowApi(payload);
        ElMessage.success('新增成功');
      }

      formDialogVisible.value = false;
      await loadWorkflows();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      formSaving.value = false;
    }
  };

  const handleDelete = async (workflow: WorkflowItem) => {
    try {
      await ElMessageBox.confirm(
        `确认删除工作流「${workflow.name}」？删除后不可恢复。`,
        '删除工作流',
        { type: 'warning' },
      );
      await deleteWorkflowApi(workflow.id);
      ElMessage.success('删除成功');
      await loadWorkflows();
    } catch {
      // 其他错误已由 request.ts 拦截器统一弹出
    }
  };

  const handleToggleStatus = async (workflow: WorkflowItem) => {
    const nextActive = !workflow.isActive;
    try {
      await ElMessageBox.confirm(
        `确认${nextActive ? '启用' : '停用'}工作流「${workflow.name}」？`,
        `${nextActive ? '启用' : '停用'}工作流`,
        { type: 'warning' },
      );
      await setWorkflowStatusApi(workflow.id, nextActive);
      ElMessage.success(`${nextActive ? '启用' : '停用'}成功`);
      await loadWorkflows();
    } catch {}
  };

  const runDesignSave = createSingleFlight(async (bpmnXml: string) => {
    const workflow = currentWorkflow.value;
    if (!workflow) return;

    designSaving.value = true;
    try {
      const previousId = workflow.id;
      const saved = await saveWorkflowDesignApi(previousId, bpmnXml);
      ElMessage.success(
        saved.id === previousId
          ? '保存成功'
          : '已创建并启用新版本，进行中的申请继续使用旧版本',
      );
      dialogVisible.value = false;
      await loadWorkflows();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      designSaving.value = false;
    }
  });

  const handleSave = async (bpmnXml: string, onComplete: () => void) => {
    try {
      await runDesignSave(bpmnXml);
    } finally {
      onComplete();
      designerExporting.value = false;
    }
  };

  const handleDesignerBeforeClose = (done: () => void) => {
    if (designerBusy.value) {
      ElMessage.warning('正在保存流程，请稍候');
      return;
    }
    done();
  };

  onMounted(() => {
    loadWorkflows();
  });

  return {
    BpmnModeler,
    workflowActionAccess,
    loading,
    workflows,
    dialogVisible,
    currentWorkflow,
    formDialogVisible,
    formSaving,
    designSaving,
    designerExporting,
    designerBusy,
    editingWorkflow,
    workflowForm,
    bizTypeOptions,
    loadWorkflows,
    openDesigner,
    openCreateDialog,
    openEditDialog,
    handleFormSave,
    handleDelete,
    handleToggleStatus,
    runDesignSave,
    handleSave,
    handleDesignerBeforeClose,
  };
}
