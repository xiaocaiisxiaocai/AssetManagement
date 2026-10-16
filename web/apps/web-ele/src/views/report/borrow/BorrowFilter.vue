<script lang="ts" setup>
import { ElButton, ElDatePicker, ElForm, ElFormItem } from 'element-plus';

type BorrowQueryState = {
  borrowerIds: number[];
  categoryIds: number[];
  dateRange: string[];
  page: number;
  pageSize: number;
  statuses: string[];
};

defineProps<{
  canExport: boolean;
  exporting: boolean;
}>();

const emit = defineEmits<{
  export: [];
  reset: [];
  search: [];
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
