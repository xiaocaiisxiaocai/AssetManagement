<script lang="ts" setup>
import type { AssetListQueryState } from '../asset-list-state';

import { ElButton, ElInput } from 'element-plus';

const emit = defineEmits<{
  reset: [];
  search: [];
}>();

const query = defineModel<AssetListQueryState>('query', { required: true });
</script>

<template>
  <div class="asset-filter-strip asset-filter-strip-final">
    <ElInput
      class="asset-filter-field"
      v-model="query.assetNo"
      clearable
      placeholder="资产编号"
      style="width: 200px"
      @keyup.enter="emit('search')"
    />
    <ElInput
      class="asset-filter-field"
      v-model="query.name"
      clearable
      placeholder="资产名称"
      style="width: 220px"
      @keyup.enter="emit('search')"
    />
    <ElButton type="primary" @click="emit('search')">查询</ElButton>
    <ElButton @click="emit('reset')">重置</ElButton>
  </div>
</template>

<style scoped src="./AssetTablePanel.css"></style>
