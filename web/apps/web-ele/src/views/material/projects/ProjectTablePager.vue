<script lang="ts" setup>
import { ElOption, ElPagination, ElSelect } from 'element-plus';

defineProps<{
  pageSizeOptions: number[];
  total: number;
}>();

const emit = defineEmits<{
  pageChange: [];
  pageSizeChange: [];
}>();
const query = defineModel<{ page: number; pageSize: number }>('query', {
  required: true,
});
</script>

<template>
  <div class="table-bottom-pager">
    <div class="table-bottom-pager-left">
      <span>共 {{ total }} 条记录</span>
      <span class="table-bottom-pager-divider">|</span>
      <span>每页</span>
      <ElSelect
        v-model="query.pageSize"
        aria-label="项目列表每页条数"
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
</template>

<style scoped src="./ProjectTable.css"></style>
