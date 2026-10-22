<script lang="ts" setup>
import type { WorkflowItem } from '#/api/workflow';
import type { TableColumnOption } from '#/components/table/column-visibility';

import { ElButton, ElTable, ElTableColumn, ElTag } from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
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

const tableColumns: TableColumnOption[] = [
  { key: 'id', label: 'ID' },
  { key: 'name', label: '名称' },
  { key: 'bizType', label: '业务类型' },
  { key: 'bpmnStatus', label: 'BPMN 状态' },
  { key: 'status', label: '状态' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('admin-workflows', tableColumns);
</script>

<template>
  <div class="table-panel page-fill table-scroll">
    <Teleport defer to="[data-column-tools='admin-workflows']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable :data="workflows" border height="100%" v-loading="loading">
      <ElTableColumn
        v-if="visibleColumn('id')"
        align="center"
        class-name="hide-on-mobile"
        label="ID"
        prop="id"
        width="80"
      />
      <ElTableColumn
        v-if="visibleColumn('name')"
        label="名称"
        min-width="180"
        prop="name"
      />
      <ElTableColumn
        v-if="visibleColumn('bizType')"
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
        v-if="visibleColumn('bpmnStatus')"
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
      <ElTableColumn
        v-if="visibleColumn('status')"
        align="center"
        label="状态"
        width="100"
      >
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="
          (access.canDesign || access.canEdit || access.canDelete) &&
          visibleColumn('actions')
        "
        align="center"
        class-name="admin-action-cell table-action-cell"
        fixed="right"
        label="操作"
        width="190"
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

<style scoped>
.table-panel :deep(.admin-action-cell .cell) {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
  justify-content: center;
  white-space: normal;
}

.table-panel :deep(.admin-action-cell .el-button + .el-button) {
  margin-left: 0;
}
</style>
