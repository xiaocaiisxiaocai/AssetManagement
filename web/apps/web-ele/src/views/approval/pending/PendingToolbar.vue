<script lang="ts" setup>
import type { ApprovalSource } from '../notification-flow-attempt';

import {
  ElButton,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElRadioButton,
  ElRadioGroup,
  ElSelect,
  ElTabPane,
  ElTabs,
} from 'element-plus';

defineProps<{
  canHandleMaterialFlow: boolean;
}>();

const emit = defineEmits<{
  reset: [];
  search: [];
  sourceChange: [];
  viewModeChange: [];
}>();

const viewMode = defineModel<'handled' | 'pending'>('viewMode', {
  required: true,
});
const activeSource = defineModel<ApprovalSource>('activeSource', {
  required: true,
});
const query = defineModel<{ bizType: string; keyword: string }>('query', {
  required: true,
});
</script>

<template>
  <div aria-label="审批记录范围" class="pending-view-switch">
    <ElRadioGroup v-model="viewMode" @change="emit('viewModeChange')">
      <ElRadioButton value="pending">待我处理</ElRadioButton>
      <ElRadioButton value="handled">我已处理</ElRadioButton>
    </ElRadioGroup>
    <span class="pending-view-tip">
      {{
        viewMode === 'pending'
          ? '仅显示当前需要您处理的审批任务'
          : '显示您已经通过或驳回的审批记录'
      }}
    </span>
  </div>

  <div class="filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="关键字">
        <ElInput
          v-model="query.keyword"
          clearable
          placeholder="流程单号/资产/料件/申请人/节点"
          style="width: 260px"
          @keyup.enter="emit('search')"
        />
      </ElFormItem>
      <ElFormItem v-if="activeSource === 'asset'" label="业务类型">
        <ElSelect
          v-model="query.bizType"
          clearable
          placeholder="全部类型"
          style="width: 140px"
        >
          <ElOption label="借用" value="borrow" />
          <ElOption label="转让" value="transfer" />
          <ElOption label="延期" value="extension" />
          <ElOption label="归还" value="return" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>

  <ElTabs v-model="activeSource" @tab-change="emit('sourceChange')">
    <ElTabPane label="资产审批" name="asset" />
    <ElTabPane
      v-if="canHandleMaterialFlow"
      label="料件审批"
      name="material"
    />
  </ElTabs>
</template>

<style scoped src="./PendingToolbar.css"></style>
