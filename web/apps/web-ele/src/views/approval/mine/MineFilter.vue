<script lang="ts" setup>
import type { ApprovalSource } from '../notification-flow-attempt';

import {
  ElButton,
  ElForm,
  ElFormItem,
  ElInput,
  ElTabPane,
  ElTabs,
} from 'element-plus';

defineProps<{
  canViewMaterialFlow: boolean;
}>();

const emit = defineEmits<{
  reset: [];
  search: [];
  sourceChange: [];
}>();

const activeSource = defineModel<ApprovalSource>('activeSource', {
  required: true,
});
const query = defineModel<{
  bizTypes: string[];
  keyword: string;
  statuses: string[];
}>('query', { required: true });
</script>

<template>
  <ElTabs v-model="activeSource" @tab-change="emit('sourceChange')">
    <ElTabPane label="资产申请" name="asset" />
    <ElTabPane
      v-if="canViewMaterialFlow"
      label="料件申请"
      name="material"
    />
  </ElTabs>

  <div class="filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="关键字">
        <ElInput
          v-model="query.keyword"
          clearable
          placeholder="流程编号/对象/借用人/接收人/事由"
          style="width: 280px"
          @keyup.enter="emit('search')"
        />
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>

<style scoped src="./MineFilter.css"></style>
