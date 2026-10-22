<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';
import type { ApprovalSource } from '../notification-flow-attempt';

import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';
import { formatDateTime } from '#/utils/date-format';

defineProps<{
  activeSource: ApprovalSource;
  flows: ApprovalWorkItem[];
  loading: boolean;
  materialActionLoadingIds: Set<number>;
  pageSizeOptions: number[];
  releasingKeys: string[];
  total: number;
  viewMode: 'handled' | 'pending';
}>();

const emit = defineEmits<{
  approveMaterial: [item: ApprovalWorkItem];
  openDetail: [item: ApprovalWorkItem];
  openHandledDetail: [item: ApprovalWorkItem];
  pageChange: [];
  pageSizeChange: [];
  rejectMaterial: [item: ApprovalWorkItem];
  release: [item: ApprovalWorkItem];
  search: [];
}>();

const query = defineModel<{ page: number; pageSize: number }>('query', {
  required: true,
});
const filters = defineModel<{ bizTypes: string[] }>('filters', {
  required: true,
});
const bizTypeOptions = [
  { label: '借用', value: 'borrow' },
  { label: '转让', value: 'transfer' },
  { label: '延期', value: 'extension' },
  { label: '归还', value: 'return' },
];

function approvalActionLabel(action: null | string | undefined) {
  return action === 'reject' ? '已驳回' : '已通过';
}

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流程单号' },
  { key: 'source', label: '来源' },
  { key: 'bizType', label: '业务类型' },
  { key: 'objectName', label: '对象名称' },
  { key: 'applicant', label: '申请人' },
  { key: 'participant', label: '借用人/接收人' },
  { key: 'applyTime', label: '申请时间' },
  { key: 'progress', label: '审批进度' },
  { key: 'myAction', label: '我的处理' },
  { key: 'myTime', label: '处理时间' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('approval-pending', tableColumns, (key, visible) => {
    if (!visible && key === 'bizType' && filters.value.bizTypes.length > 0) {
      filters.value.bizTypes = [];
      emit('search');
    }
  });
</script>

<template>
  <div class="pending-table-panel page-fill table-scroll">
    <Teleport defer to="[data-column-tools='approval-pending']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable :data="flows" border height="100%" v-loading="loading">
      <ElTableColumn
        v-if="visibleColumn('flowNo')"
        label="流程单号"
        prop="flowNo"
        width="180"
      />
      <ElTableColumn
        v-if="visibleColumn('source')"
        align="center"
        class-name="hide-on-mobile"
        label="来源"
        width="110"
      >
        <template #default="{ row }">
          <ElTag
            :type="row.source === 'asset' ? 'primary' : 'success'"
            size="small"
          >
            {{ row.sourceLabel }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('bizType')"
        align="center"
        class-name="hide-on-mobile"
        label="业务类型"
        prop="bizType"
        width="130"
      >
        <template #header>
          <HeaderMultiFilter
            v-if="activeSource === 'asset'"
            v-model="filters.bizTypes"
            :options="bizTypeOptions"
            label="业务类型"
            @change="emit('search')"
          />
          <span v-else>业务类型</span>
        </template>
        <template #default="{ row }">
          <ElTag v-if="row.bizType === 'borrow'" size="small" type="success">
            {{ row.typeLabel }}
          </ElTag>
          <ElTag
            v-else-if="row.bizType === 'transfer'"
            size="small"
            type="warning"
          >
            {{ row.typeLabel }}
          </ElTag>
          <ElTag
            v-else-if="row.bizType === 'material_transfer'"
            size="small"
            type="primary"
          >
            {{ row.typeLabel }}
          </ElTag>
          <ElTag v-else size="small" type="info">
            {{ row.typeLabel }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('objectName')"
        label="对象名称"
        min-width="160"
        prop="objectName"
      />
      <ElTableColumn
        v-if="visibleColumn('applicant')"
        class-name="hide-on-mobile"
        label="申请人"
        prop="applicant"
        width="120"
      />
      <ElTableColumn
        v-if="visibleColumn('participant')"
        class-name="hide-on-mobile"
        label="借用人/接收人"
        prop="participant"
        width="140"
      />
      <ElTableColumn
        v-if="visibleColumn('applyTime')"
        class-name="hide-on-mobile"
        label="申请时间"
        width="170"
      >
        <template #default="{ row }">
          {{ formatDateTime(row.applyTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('progress')"
        label="审批进度"
        min-width="320"
      >
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.raw.currentSteps"
            :next-steps="row.raw.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="viewMode === 'handled' && visibleColumn('myAction')"
        align="center"
        label="我的处理"
        width="110"
      >
        <template #default="{ row }">
          <ElTag
            :type="row.myApprovalAction === 'reject' ? 'danger' : 'success'"
            effect="plain"
            size="small"
          >
            {{ approvalActionLabel(row.myApprovalAction) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="viewMode === 'handled' && visibleColumn('myTime')"
        class-name="hide-on-mobile"
        label="处理时间"
        width="170"
      >
        <template #default="{ row }">
          {{ formatDateTime(row.myApprovalTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="180"
      >
        <template #default="{ row }">
          <ElButton
            v-if="viewMode === 'handled'"
            link
            size="small"
            type="primary"
            @click="emit('openHandledDetail', row)"
          >
            查看记录
          </ElButton>
          <ElButton
            v-else-if="row.canReleaseStuck"
            :loading="releasingKeys.includes(row.key)"
            link
            size="small"
            type="danger"
            @click="emit('release', row)"
          >
            终止并重新发起
          </ElButton>
          <ElButton
            v-else-if="row.source === 'asset'"
            link
            size="small"
            type="primary"
            @click="emit('openDetail', row)"
          >
            审批
          </ElButton>
          <template v-else>
            <ElButton
              :disabled="row.actionableNodeIds.length === 0"
              :loading="materialActionLoadingIds.has(row.id)"
              link
              size="small"
              type="success"
              @click="emit('approveMaterial', row)"
            >
              通过
            </ElButton>
            <ElButton
              :disabled="row.actionableNodeIds.length === 0"
              :loading="materialActionLoadingIds.has(row.id)"
              link
              size="small"
              type="danger"
              @click="emit('rejectMaterial', row)"
            >
              驳回
            </ElButton>
          </template>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ total }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="query.pageSize"
          style="width: 92px"
          @change="emit('pageSizeChange')"
        >
          <ElOption
            v-for="size in pageSizeOptions"
            :key="size"
            :label="`${size}`"
            :value="size"
          />
        </ElSelect>
      </div>
      <ElPagination
        v-model:current-page="query.page"
        :page-size="query.pageSize"
        :total="total"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped src="./PendingTable.css"></style>
