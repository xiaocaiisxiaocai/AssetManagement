<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';
import type { UserOptionDto } from '#/api/user';

import {
  ElButton,
  ElDatePicker,
  ElForm,
  ElFormItem,
  ElOption,
  ElSelect,
} from 'element-plus';

type BorrowQueryState = {
  borrowerId: number | undefined;
  categoryId: number | undefined;
  dateRange: string[];
  page: number;
  pageSize: number;
  status: string | undefined;
};

defineProps<{
  borrowerOptions: UserOptionDto[];
  canExport: boolean;
  categoryOptions: CategoryNode[];
  exporting: boolean;
  userOptionsLoading: boolean;
}>();

const emit = defineEmits<{
  export: [];
  reset: [];
  search: [];
  userSearch: [keyword: string];
}>();
const query = defineModel<BorrowQueryState>('query', { required: true });
</script>

<template>
  <div class="filter-panel borrow-filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="申请时间">
        <ElDatePicker
          v-model="query.dateRange"
          end-placeholder="结束日期"
          format="YYYY-MM-DD"
          range-separator="至"
          start-placeholder="开始日期"
          style="width: 240px"
          type="daterange"
          value-format="YYYY-MM-DD"
        />
      </ElFormItem>
      <ElFormItem label="借用人">
        <ElSelect
          v-model="query.borrowerId"
          :loading="userOptionsLoading"
          :remote-method="(keyword: string) => emit('userSearch', keyword)"
          aria-label="借用人"
          clearable
          filterable
          placeholder="选择借用人"
          remote
          style="width: 180px"
        >
          <ElOption
            v-for="user in borrowerOptions"
            :key="user.id"
            :label="`${user.name}（${user.employeeNo}）`"
            :value="user.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="分类">
        <ElSelect
          v-model="query.categoryId"
          aria-label="资产分类"
          clearable
          filterable
          placeholder="选择分类"
          style="width: 180px"
        >
          <ElOption
            v-for="category in categoryOptions"
            :key="category.id"
            :label="category.code"
            :value="category.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="状态">
        <ElSelect
          v-model="query.status"
          aria-label="借用状态"
          clearable
          placeholder="全部状态"
          style="width: 130px"
        >
          <ElOption label="借用中" value="borrowed" />
          <ElOption label="已归还" value="returned" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
        <ElButton v-if="canExport" :loading="exporting" @click="emit('export')">
          导出
        </ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>

<style scoped src="./BorrowFilter.css"></style>
