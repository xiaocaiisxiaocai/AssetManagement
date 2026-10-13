<script lang="ts" setup>
import type { DeleteStatus } from './project-workspace-types';

import type { MaterialStatus } from '#/api/material';

import { ElButton, ElInput, ElOption, ElSelect } from 'element-plus';

defineProps<{
  canCreate: boolean;
  statusOptions: Array<{
    label: string;
    tag: 'info' | 'success';
    value: MaterialStatus;
  }>;
}>();

const emit = defineEmits<{
  create: [];
  reset: [];
  search: [];
}>();
const query = defineModel<{
  deleteStatus: DeleteStatus;
  materialNo: string;
  name: string;
  page: number;
  pageSize: number;
  status?: MaterialStatus;
}>('query', { required: true });
</script>

<template>
  <div class="material-filter">
    <ElInput
      v-model="query.materialNo"
      aria-label="料件编号"
      clearable
      placeholder="料件编号"
      style="width: 150px"
      @keyup.enter="emit('search')"
    />
    <ElInput
      v-model="query.name"
      aria-label="料件名称"
      clearable
      placeholder="料件名称"
      style="width: 160px"
      @keyup.enter="emit('search')"
    />
    <ElSelect
      v-model="query.status"
      aria-label="料件状态"
      clearable
      placeholder="状态"
      style="width: 120px"
    >
      <ElOption
        v-for="item in statusOptions"
        :key="item.value"
        :label="item.label"
        :value="item.value"
      />
    </ElSelect>
    <ElSelect
      v-model="query.deleteStatus"
      aria-label="料件删除状态"
      placeholder="删除状态"
      style="width: 120px"
      @change="emit('search')"
    >
      <ElOption label="全部" value="all" />
      <ElOption label="未删除" value="active" />
      <ElOption label="已删除" value="deleted" />
    </ElSelect>
    <ElButton type="primary" @click="emit('search')">查询</ElButton>
    <ElButton @click="emit('reset')">重置</ElButton>
    <ElButton v-if="canCreate" type="primary" @click="emit('create')">
      新增料件
    </ElButton>
  </div>
</template>

<style scoped src="./ProjectMaterialsTab.css"></style>
