<script lang="ts" setup>
import { defineAsyncComponent, onMounted, ref } from 'vue';
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
  type SaveWorkflowPayload,
  type WorkflowItem,
} from '#/api/workflow';

defineOptions({ name: 'AdminWorkflows' });

const BpmnModeler = defineAsyncComponent(() => import('./bpmn-modeler.vue'));

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
  } catch (error: any) {
    ElMessage.error(error.message || '加载工作流失败');
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
  } catch (error: any) {
    ElMessage.error(error.message || '保存失败');
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
    ElMessage.error(error.message || '删除失败');
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
  } catch (error: any) {
    ElMessage.error(error.message || '保存失败');
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
          <p class="page-subtitle">BPMN 2.0 可视化审批流程配置</p>
        </div>
        <div class="page-actions">
          <ElButton @click="loadWorkflows" :loading="loading">刷新</ElButton>
          <ElButton type="primary" @click="openCreateDialog">新增工作流</ElButton>
        </div>
      </div>

      <div class="table-panel">
        <ElTable :data="workflows" v-loading="loading" border>
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
          <ElTableColumn label="操作" width="240" align="center" fixed="right">
            <template #default="{ row }">
              <ElButton type="primary" link size="small" @click="openDesigner(row)">
                设计流程
              </ElButton>
              <ElButton type="primary" link size="small" @click="openEditDialog(row)">
                编辑
              </ElButton>
              <ElButton type="danger" link size="small" @click="handleDelete(row)">
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
        :title="`设计工作流: ${currentWorkflow?.name}`"
        width="90%"
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

<style scoped>
/* BPMN 设计器对话框保持全宽 */
</style>
