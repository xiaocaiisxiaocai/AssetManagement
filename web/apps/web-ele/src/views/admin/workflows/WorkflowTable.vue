<script lang="ts" setup>
import type { WorkflowItem } from '#/api/workflow';

import { ElButton, ElTable, ElTableColumn, ElTag } from 'element-plus';

import { buildWorkflowActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildWorkflowActionAccess>;
  loading: boolean;
  workflows: WorkflowItem[];
}>();

const emit = defineEmits<{
  delete: [workflow: WorkflowItem];
  design: [workflow: WorkflowItem];
  edit: [workflow: WorkflowItem];
  toggleStatus: [workflow: WorkflowItem];
}>();

const bpmnStatusMap = {
  configured: { label: '已配置', type: 'success' },
  empty: { label: '未配置', type: 'warning' },
  invalid: { label: '配置异常', type: 'danger' },
} as const;

function bizTypeLabel(workflow: WorkflowItem) {
  return workflow.bizTypeLabel || workflow.bizType;
}

function bpmnStatusMeta(status: WorkflowItem['bpmnStatus']) {
  return bpmnStatusMap[status] ?? bpmnStatusMap.empty;
}
</script>

<template>
  <div class="table-panel">
    <ElTable :data="workflows" border height="100%" v-loading="loading">
      <ElTableColumn
        align="center"
        class-name="hide-on-mobile"
        label="ID"
        prop="id"
        width="80"
      />
      <ElTableColumn label="名称" min-width="180" prop="name" />
      <ElTableColumn
        align="center"
        label="业务类型"
        prop="bizType"
        width="120"
      >
        <template #default="{ row }">
          <ElTag size="small">{{ bizTypeLabel(row) }}</ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        align="center"
        class-name="hide-on-mobile"
        label="BPMN 状态"
        width="120"
      >
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
      <ElTableColumn align="center" label="状态" width="100">
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="access.canDesign || access.canEdit || access.canDelete"
        align="center"
        fixed="right"
        label="操作"
        width="290"
      >
        <template #default="{ row }">
          <ElButton
            v-if="access.canDesign"
            link
            size="small"
            type="primary"
            @click="emit('design', row)"
          >
            设计流程
          </ElButton>
          <ElButton
            v-if="access.canEdit"
            link
            size="small"
            type="primary"
            @click="emit('edit', row)"
          >
            编辑
          </ElButton>
          <ElButton
            v-if="access.canEdit"
            :type="row.isActive ? 'warning' : 'success'"
            link
            size="small"
            @click="emit('toggleStatus', row)"
          >
            {{ row.isActive ? '停用' : '启用' }}
          </ElButton>
          <ElButton
            v-if="access.canDelete"
            link
            size="small"
            type="danger"
            @click="emit('delete', row)"
          >
            删除
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
  </div>
</template>
