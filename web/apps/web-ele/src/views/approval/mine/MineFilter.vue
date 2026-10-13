<script lang="ts" setup>
import type { ApprovalSource } from '../notification-flow-attempt';

import {
  ElButton,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
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
  bizType: string;
  keyword: string;
  status: string;
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
      <ElFormItem v-if="activeSource === 'asset'" label="业务类型">
        <ElSelect
          v-model="query.bizType"
          clearable
          placeholder="全部类型"
          style="width: 150px"
        >
          <ElOption label="借用" value="borrow" />
          <ElOption label="转让" value="transfer" />
          <ElOption label="延期" value="extension" />
          <ElOption label="归还" value="return" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="状态">
        <ElSelect
          v-model="query.status"
          clearable
          placeholder="全部状态"
          style="width: 130px"
        >
          <ElOption label="审批中" value="pending" />
          <ElOption label="已通过" value="approved" />
          <ElOption label="已驳回" value="rejected" />
          <ElOption label="已撤回" value="withdrawn" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>

<style scoped src="./MineFilter.css"></style>
