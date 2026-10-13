<script lang="ts" setup>
import type { AssetListQueryState } from '../asset-list-state';

import type { AssetStatus } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import { ElButton, ElInput, ElOption, ElSelect } from 'element-plus';

defineProps<{
  departmentOptions: { id: number; label: string }[];
  userOptionsLoading: boolean;
  users: UserOptionDto[];
}>();

const emit = defineEmits<{
  reset: [];
  search: [];
  userSearch: [keyword: string];
}>();

const query = defineModel<AssetListQueryState>('query', { required: true });

const statusOptions: Array<{
  label: string;
  tag: 'danger' | 'info' | 'success' | 'warning';
  value: AssetStatus;
}> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
];
</script>

<template>
  <div class="asset-filter-strip asset-filter-strip-final">
    <ElInput
      v-model="query.assetNo"
      clearable
      placeholder="资产编号"
      style="width: 200px"
      @keyup.enter="emit('search')"
    />
    <ElInput
      v-model="query.name"
      clearable
      placeholder="资产名称"
      style="width: 220px"
      @keyup.enter="emit('search')"
    />
    <ElSelect
      v-model="query.status"
      clearable
      placeholder="状态"
      style="width: 110px"
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
      placeholder="删除状态"
      style="width: 130px"
      @change="emit('search')"
    >
      <ElOption label="全部" value="all" />
      <ElOption label="未删除" value="active" />
      <ElOption label="已删除" value="deleted" />
    </ElSelect>
    <ElSelect
      v-model="query.custodianId"
      :loading="userOptionsLoading"
      :remote-method="(keyword: string) => emit('userSearch', keyword)"
      clearable
      filterable
      placeholder="保管人"
      remote
      style="width: 180px"
    >
      <ElOption
        v-for="user in users"
        :key="user.id"
        :label="`${user.name}（${user.employeeNo}）`"
        :value="user.id"
      />
    </ElSelect>
    <ElSelect
      v-model="query.departmentId"
      clearable
      filterable
      placeholder="归属部门"
      style="width: 180px"
    >
      <ElOption
        v-for="item in departmentOptions"
        :key="item.id"
        :label="item.label"
        :value="item.id"
      />
    </ElSelect>
    <ElButton type="primary" @click="emit('search')">查询</ElButton>
    <ElButton @click="emit('reset')">重置</ElButton>
  </div>
</template>

<style scoped src="./AssetTablePanel.css"></style>
