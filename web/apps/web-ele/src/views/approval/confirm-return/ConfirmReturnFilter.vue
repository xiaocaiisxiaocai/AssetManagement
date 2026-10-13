<script lang="ts" setup>
import {
  ElButton,
  ElDatePicker,
  ElForm,
  ElFormItem,
  ElInput,
} from 'element-plus';

const emit = defineEmits<{
  reset: [];
  search: [];
}>();

const query = defineModel<{
  keyword: string;
  page: number;
  pageSize: number;
  returnDate: string;
}>('query', { required: true });
</script>

<template>
  <div class="filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="关键字">
        <ElInput
          v-model="query.keyword"
          clearable
          placeholder="流程编号/资产/借用人"
          style="width: 260px"
          @keyup.enter="emit('search')"
        />
      </ElFormItem>
      <ElFormItem label="应归还日期">
        <ElDatePicker
          v-model="query.returnDate"
          clearable
          placeholder="选择日期"
          style="width: 160px"
          type="date"
          value-format="YYYY-MM-DD"
          @change="emit('search')"
        />
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>
