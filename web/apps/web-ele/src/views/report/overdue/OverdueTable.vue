<script lang="ts" setup>
import type { OverdueReportRow } from '#/api/report';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

type OverdueQueryState = {
  page: number;
  pageSize: number;
};

defineProps<{
  canRemind: boolean;
  loading: boolean;
  pageSizeOptions: number[];
  remindingId: null | number;
  rows: OverdueReportRow[];
  total: number;
}>();

const emit = defineEmits<{
  categoryClick: [categoryCode: string];
  pageChange: [];
  pageSizeChange: [];
  remind: [row: OverdueReportRow];
  selectionChange: [selection: OverdueReportRow[]];
}>();
const query = defineModel<OverdueQueryState>('query', { required: true });
</script>

<template>
  <div class="table-panel">
    <ElTable
      :data="rows"
      border
      height="100%"
      row-key="assetId"
      v-loading="loading"
      @selection-change="emit('selectionChange', $event)"
    >
      <ElTableColumn v-if="canRemind" type="selection" width="48" />
      <ElTableColumn label="资产" min-width="220">
        <template #default="{ row }">
          <div>{{ row.assetName }}</div>
          <ElTag size="small">{{ row.assetNo }}</ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn class-name="hide-on-mobile" label="分类" min-width="170">
        <template #default="{ row }">
          <button
            v-if="row.categoryCode"
            class="category-code-link"
            type="button"
            @click="emit('categoryClick', row.categoryCode)"
          >
            {{ row.categoryCode }}
          </button>
          <span v-else class="empty-text">-</span>
        </template>
      </ElTableColumn>
      <ElTableColumn label="借用人" min-width="120" prop="borrower" />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="部门"
        min-width="120"
        prop="borrowerDept"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="预计归还"
        min-width="120"
        prop="returnDate"
      />
      <ElTableColumn align="center" label="逾期天数" width="120">
        <template #default="{ row }">
          <ElTag :type="row.isSerious ? 'danger' : 'warning'" size="small">
            {{ row.overdueDays }} 天
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="canRemind"
        align="center"
        fixed="right"
        label="操作"
        width="110"
      >
        <template #default="{ row }">
          <ElButton
            :loading="remindingId === row.assetId"
            link
            size="small"
            type="warning"
            @click="emit('remind', row)"
          >
            催办
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
