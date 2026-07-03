<script lang="ts" setup>
import { computed, defineAsyncComponent, onMounted, ref } from 'vue';
import { useAccess } from '@vben/access';
import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';
import {
  createWorkflowApi,
  deleteWorkflowApi,
  getWorkflowsApi,
  saveWorkflowApi,
  setWorkflowStatusApi,
  type SaveWorkflowPayload,
  type WorkflowItem,
} from '#/api/workflow';
import { buildWorkflowActionAccess } from '#/views/permissions/action-access';

defineOptions({ name: 'AdminWorkflows' });

const BpmnModeler = defineAsyncComponent(() => import('./bpmn-modeler.vue'));

const { hasAccessByCodes } = useAccess();
const workflowActionAccess = computed(() => buildWorkflowActionAccess(hasAccessByCodes));
const loading = ref(false);
const workflows = ref<WorkflowItem[]>([]);
const dialogVisible = ref(false);
const currentWorkflow = ref<WorkflowItem | null>(null);
const formDialogVisible = ref(false);
const formSaving = ref(false);
const editingWorkflow = ref<WorkflowItem | null>(null);
const workflowForm = ref<SaveWorkflowPayload>({
  bizType: '',
  bpmnXml: null,
  name: '',
});

const bizTypeOptions = [
  { label: '资产借用', value: 'borrow' },
  { label: '资产转让', value: 'transfer' },
  { label: '资产归还', value: 'return' },
  { label: '测试料件流转', value: 'material_transfer' },
];

const bpmnStatusMap = {
  configured: { label: '已配置', type: 'success' },
  empty: { label: '未配置', type: 'warning' },
  invalid: { label: '配置异常', type: 'danger' },
} as const;

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
  } catch (error: any) {
    if (error === 'cancel' || error === 'close') return;
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
  } catch (error: any) {
    if (error === 'cancel' || error === 'close') return;
  }
};

const handleSave = async (bpmnXml: string) => {
  if (!currentWorkflow.value) return;

  try {
    await saveWorkflowApi(currentWorkflow.value.id, {
      name: currentWorkflow.value.name,
      bizType: currentWorkflow.value.bizType,
      bpmnXml,
    });
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadWorkflows();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
};

const bizTypeLabel = (workflow: WorkflowItem) =>
  workflow.bizTypeLabel || workflow.bizType;

const bpmnStatusMeta = (status: WorkflowItem['bpmnStatus']) =>
  bpmnStatusMap[status] ?? bpmnStatusMap.empty;

onMounted(() => {
  loadWorkflows();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">工作流设计器</h2>
        </div>
        <div class="page-actions">
          <ElButton v-if="workflowActionAccess.canCreate" type="primary" @click="openCreateDialog">新增工作流</ElButton>
        </div>
      </div>

      <div class="table-panel">
        <ElTable :data="workflows" v-loading="loading" border height="100%">
          <ElTableColumn class-name="hide-on-mobile" prop="id" label="ID" width="80" align="center" />
          <ElTableColumn prop="name" label="名称" min-width="180" />
          <ElTableColumn prop="bizType" label="业务类型" width="120" align="center">
            <template #default="{ row }">
              <ElTag size="small">{{ bizTypeLabel(row) }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="BPMN 状态" width="120" align="center">
            <template #default="{ row }">
              <ElTag
                :title="row.bpmnValidationErrors?.join('；')"
                :type="bpmnStatusMeta(row.bpmnStatus).type"
                size="small"
              >
                {{ bpmnStatusMeta(row.bpmnStatus).label }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? '启用' : '停用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn v-if="workflowActionAccess.canDesign || workflowActionAccess.canEdit || workflowActionAccess.canDelete" label="操作" width="290" align="center" fixed="right">
            <template #default="{ row }">
              <ElButton v-if="workflowActionAccess.canDesign" type="primary" link size="small" @click="openDesigner(row)">
                设计流程
              </ElButton>
              <ElButton v-if="workflowActionAccess.canEdit" type="primary" link size="small" @click="openEditDialog(row)">
                编辑
              </ElButton>
              <ElButton
                v-if="workflowActionAccess.canEdit"
                :type="row.isActive ? 'warning' : 'success'"
                link
                size="small"
                @click="handleToggleStatus(row)"
              >
                {{ row.isActive ? '停用' : '启用' }}
              </ElButton>
              <ElButton v-if="workflowActionAccess.canDelete" type="danger" link size="small" @click="handleDelete(row)">
                删除
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>

      <ElDialog
        v-model="formDialogVisible"
        :title="editingWorkflow ? '编辑工作流' : '新增工作流'"
        width="520px"
        :close-on-click-modal="false"
      >
        <ElForm label-width="90px" label-position="top">
          <ElFormItem label="流程名称" required>
            <ElInput v-model="workflowForm.name" placeholder="如：资产借用流程" />
          </ElFormItem>
          <ElFormItem label="业务类型" required>
            <ElSelect
              v-model="workflowForm.bizType"
              allow-create
              filterable
              placeholder="请选择业务类型"
              style="width: 100%"
            >
              <ElOption
                v-for="item in bizTypeOptions"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              />
            </ElSelect>
          </ElFormItem>
        </ElForm>

        <template #footer>
          <ElButton @click="formDialogVisible = false">取消</ElButton>
          <ElButton type="primary" :loading="formSaving" @click="handleFormSave">
            保存
          </ElButton>
        </template>
      </ElDialog>

      <!-- BPMN 设计器对话框 -->
      <ElDialog
        v-model="dialogVisible"
        class="workflow-designer-dialog"
        :title="`流程设计 - ${currentWorkflow?.name}`"
        fullscreen
        :close-on-click-modal="false"
        destroy-on-close
      >
        <BpmnModeler
          v-if="dialogVisible && currentWorkflow"
          :workflow-id="currentWorkflow.id"
          :initial-xml="currentWorkflow.bpmnXml || undefined"
          @save="handleSave"
        />
      </ElDialog>
    </div>
  </re-page>
</template>

<style>
.workflow-designer-dialog {
  --el-dialog-bg-color: #eef3f9;
  background: #eef3f9;
}

.dark .workflow-designer-dialog {
  --el-dialog-bg-color: var(--el-bg-color-page);
  background: var(--el-bg-color-page);
}

.workflow-designer-dialog .el-dialog__header {
  height: 44px;
  padding: 0 16px;
  margin: 0;
  display: flex;
  align-items: center;
  border-bottom: 1px solid #d8e0eb;
  background: #ffffff;
}

.dark .workflow-designer-dialog .el-dialog__header {
  border-bottom-color: var(--el-border-color);
  background: var(--el-bg-color);
}

.workflow-designer-dialog .el-dialog__title {
  color: #1f3f6d;
  font-size: 15px;
  font-weight: 600;
}

.dark .workflow-designer-dialog .el-dialog__title {
  color: var(--el-text-color-primary);
}

.workflow-designer-dialog .el-dialog__headerbtn {
  top: 4px;
}

.workflow-designer-dialog .el-dialog__body {
  height: calc(100vh - 44px);
  padding: 0;
}
</style>
