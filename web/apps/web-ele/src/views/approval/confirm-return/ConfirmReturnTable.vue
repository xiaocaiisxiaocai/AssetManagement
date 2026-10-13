<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

defineProps<{
  confirmingIds: Set<number>;
  flows: ApprovalFlow[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
}>();

const emit = defineEmits<{
  confirm: [row: ApprovalFlow];
  pageChange: [];
  pageSizeChange: [];
}>();

const query = defineModel<{
  keyword: string;
  page: number;
  pageSize: number;
  returnDate: string;
}>('query', { required: true });

function bizText(type: string) {
  return (
    { borrow: '借用', extension: '延期', return: '归还', transfer: '转让' }[
      type
    ] ?? type
  );
}
</script>

<template>
  <div class="table-panel-with-toolbar">
    <ElTable :data="flows" border height="100%" v-loading="loading">
      <ElTableColumn label="流程编号" min-width="160" prop="flowNo" />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="资产编号"
        min-width="140"
        prop="assetNo"
      />
      <ElTableColumn label="资产名称" min-width="200" prop="assetName" />
      <ElTableColumn align="center" label="业务类型" width="100">
        <template #default="{ row }">
          <ElTag size="small" type="success">
            {{ bizText(row.bizType) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="借用人" min-width="130" prop="applicant" />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="借用部门"
        min-width="150"
        prop="applicantDept"
      />
      <ElTableColumn label="应归还日期" min-width="140" prop="returnDate" />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="申请时间"
        min-width="180"
      >
        <template #default="{ row }">
          {{ formatDateTime(row.applyTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="借用事由"
        min-width="200"
        prop="reason"
        show-overflow-tooltip
      />
      <ElTableColumn align="center" fixed="right" label="操作" width="120">
        <template #default="{ row }">
          <ElButton
            :loading="confirmingIds.has(row.id)"
            link
            size="small"
            type="primary"
            @click="emit('confirm', row)"
          >
            确认已收回
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
