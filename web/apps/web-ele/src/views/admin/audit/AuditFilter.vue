<script lang="ts" setup>
import type { AuditListQuery } from './audit-query';

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
const query = defineModel<AuditListQuery>('query', { required: true });
</script>

<template>
  <div class="filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="时间">
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
      <ElFormItem label="模块">
        <ElInput
          v-model="query.module"
          clearable
          placeholder="模块/摘要关键字"
          style="width: 180px"
        />
      </ElFormItem>
      <ElFormItem label="用户ID">
        <ElInput
          clearable
          placeholder="用户ID"
          style="width: 120px"
          v-model.number="query.userId"
        />
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>
