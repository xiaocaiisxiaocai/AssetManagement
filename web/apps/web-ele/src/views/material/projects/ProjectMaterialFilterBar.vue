<script lang="ts" setup>
import type { DeleteStatus } from './project-workspace-types';

import type { MaterialStatus } from '#/api/material';

import { ElButton, ElInput } from 'element-plus';

defineProps<{
  canCreate: boolean;
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
  statuses: MaterialStatus[];
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
    <ElButton type="primary" @click="emit('search')">查询</ElButton>
    <ElButton @click="emit('reset')">重置</ElButton>
    <ElButton v-if="canCreate" type="primary" @click="emit('create')">
      新增料件
    </ElButton>
  </div>
</template>

<style scoped src="./ProjectMaterialsTab.css"></style>
