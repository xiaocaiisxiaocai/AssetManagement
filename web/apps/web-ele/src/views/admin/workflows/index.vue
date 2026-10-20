<script lang="ts" setup>
import { ElDialog } from 'element-plus';

import { useWorkflowAdmin } from './use-workflow-admin';
import WorkflowFormDialog from './WorkflowFormDialog.vue';
import WorkflowHeader from './WorkflowHeader.vue';
import WorkflowTable from './WorkflowTable.vue';

defineOptions({ name: 'AdminWorkflows' });

const {
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
  openDesigner,
  openCreateDialog,
  openEditDialog,
  handleFormSave,
  handleDelete,
  handleToggleStatus,
  handleSave,
  handleDesignerBeforeClose,
} = useWorkflowAdmin();
</script>

<template>
  <re-page>
    <div class="page-container page-fit">
      <WorkflowHeader
        :access="workflowActionAccess"
        @create="openCreateDialog"
      />
      <WorkflowTable
        :access="workflowActionAccess"
        :loading="loading"
        :workflows="workflows"
        @delete="handleDelete"
        @design="openDesigner"
        @edit="openEditDialog"
        @toggle-status="handleToggleStatus"
      />
      <WorkflowFormDialog
        v-model:form="workflowForm"
        v-model:visible="formDialogVisible"
        :biz-type-options="bizTypeOptions"
        :editing="!!editingWorkflow"
        :saving="formSaving"
        @save="handleFormSave"
      />
      <!-- BPMN 设计器对话框 -->
      <ElDialog
        v-model="dialogVisible"
        :before-close="handleDesignerBeforeClose"
        :close-on-click-modal="false"
        :close-on-press-escape="!designerBusy"
        :show-close="!designerBusy"
        :title="`流程设计 - ${currentWorkflow?.name}`"
        class="workflow-designer-dialog"
        destroy-on-close
        fullscreen
      >
        <BpmnModeler
          v-if="dialogVisible && currentWorkflow"
          :initial-xml="currentWorkflow.bpmnXml || undefined"
          :saving="designSaving"
          :workflow-id="currentWorkflow.id"
          @exporting-change="designerExporting = $event"
          @save="handleSave"
        />
      </ElDialog>
    </div>
  </re-page>
</template>

<style src="./index.css"></style>
