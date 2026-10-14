<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';

import { canWithdrawApproval } from '../approval-work-items';

defineProps<{
  flows: ApprovalWorkItem[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
  withdrawingKeys: string[];
}>();

const emit = defineEmits<{
  pageChange: [];
  pageSizeChange: [];
  showProgress: [row: ApprovalWorkItem];
  withdraw: [row: ApprovalWorkItem];
}>();

const query = defineModel<{ page: number; pageSize: number }>('query', {
  required: true,
});

function statusText(status: string) {
  return (
    {
      approved: '已通过',
      pending: '审批中',
      rejected: '已驳回',
      withdrawn: '已撤回',
    }[status] ?? status
  );
}
</script>

<template>
  <div class="mine-table-panel page-fill">
    <ElTable :data="flows" border height="100%" v-loading="loading">
      <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
      <ElTableColumn align="center" label="来源" width="110">
        <template #default="{ row }">
          <ElTag
            :type="row.source === 'asset' ? 'primary' : 'success'"
            size="small"
          >
            {{ row.sourceLabel }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="类型" width="100">
        <template #default="{ row }">
          <ElTag
            :type="
              row.bizType === 'borrow'
                ? 'success'
                : row.bizType === 'extension'
                  ? 'primary'
                  : row.bizType === 'transfer'
                    ? 'warning'
                    : row.bizType === 'material_transfer'
                      ? 'primary'
                      : 'info'
            "
            size="small"
          >
            {{ row.typeLabel }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="对象" min-width="180" prop="objectName" />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="借用人/接收人"
        min-width="140"
        prop="participant"
      />
      <ElTableColumn label="审批进度" min-width="310">
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.raw.currentSteps"
            :next-steps="row.raw.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="状态" width="100">
        <template #default="{ row }">
          <ElTag
            :type="
              row.status === 'approved'
                ? 'success'
                : row.status === 'rejected'
                  ? 'danger'
                  : row.status === 'withdrawn'
                    ? 'info'
                    : 'warning'
            "
            size="small"
          >
            {{ statusText(row.status) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="申请事由"
        min-width="220"
        prop="reason"
      />
      <ElTableColumn align="center" fixed="right" label="操作" width="130">
        <template #default="{ row }">
          <ElButton link type="primary" @click="emit('showProgress', row)">
            流程
          </ElButton>
          <ElButton
            v-if="canWithdrawApproval(row)"
            :loading="withdrawingKeys.includes(row.key)"
            link
            type="danger"
            @click="emit('withdraw', row)"
          >
            撤回
          </ElButton>
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

<style scoped src="./MineTable.css"></style>
