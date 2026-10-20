<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';
import type { BorrowReportRow } from '#/api/report';
import type { UserOptionDto } from '#/api/user';

import { computed } from 'vue';

import {
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
import { formatDateTime } from '#/utils/date-format';

type BorrowQueryState = {
  borrowerIds: number[];
  categoryIds: number[];
  dateRange: string[];
  page: number;
  pageSize: number;
  statuses: string[];
};

const props = defineProps<{
  borrowerOptions: UserOptionDto[];
  categoryOptions: CategoryNode[];
  loading: boolean;
  pageSizeOptions: number[];
  rows: BorrowReportRow[];
  total: number;
  userOptionsLoading: boolean;
}>();

const emit = defineEmits<{
  categoryClick: [categoryCode: string];
  pageChange: [];
  pageSizeChange: [];
  search: [];
  userSearch: [keyword: string];
}>();
const query = defineModel<BorrowQueryState>('query', { required: true });

const borrowerFilterOptions = computed(() =>
  props.borrowerOptions.map((user) => ({
    label: `${user.name}（${user.employeeNo}）`,
    value: user.id,
  })),
);
const categoryFilterOptions = computed(() =>
  props.categoryOptions.map((category) => ({
    label: category.code,
    value: category.id,
  })),
);
const borrowStatusOptions = [
  { label: '借用中', value: 'borrowed' },
  { label: '已归还', value: 'returned' },
];

function statusText(status: string) {
  return status === 'returned' ? '已归还' : '借用中';
}

function statusType(status: string) {
  return status === 'returned' ? 'success' : 'warning';
}
</script>

<template>
  <div class="table-panel-with-toolbar page-fill table-scroll">
    <ElTable :data="rows" border height="100%" v-loading="loading">
      <ElTableColumn
        class-name="hide-on-mobile"
        label="流程编号"
        min-width="180"
        prop="flowNo"
      />
      <ElTableColumn label="资产" min-width="220">
        <template #default="{ row }">
          <div>{{ row.assetName }}</div>
          <ElTag size="small">{{ row.assetNo }}</ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn class-name="hide-on-mobile" label="分类" min-width="170">
        <template #header>
          <HeaderMultiFilter
            v-model="query.categoryIds"
            :options="categoryFilterOptions"
            filterable
            label="分类"
            @change="emit('search')"
          />
        </template>
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
      <ElTableColumn label="借用人" min-width="140" prop="borrower">
        <template #header>
          <HeaderMultiFilter
            v-model="query.borrowerIds"
            :loading="userOptionsLoading"
            :options="borrowerFilterOptions"
            filterable
            label="借用人"
            remote
            @change="emit('search')"
            @search="emit('userSearch', $event)"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="部门"
        min-width="120"
        prop="borrowerDept"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="申请时间"
        min-width="160"
      >
        <template #default="{ row }">
          {{ formatDateTime(row.applyTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="预计归还"
        min-width="120"
        prop="returnDate"
      />
      <ElTableColumn align="center" label="状态" width="110">
        <template #header>
          <HeaderMultiFilter
            v-model="query.statuses"
            :options="borrowStatusOptions"
            label="状态"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          <ElTag :type="statusType(row.status)" size="small">
            {{ statusText(row.status) }}
          </ElTag>
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
          aria-label="借用明细每页条数"
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
