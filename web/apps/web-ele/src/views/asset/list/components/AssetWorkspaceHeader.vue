<script lang="ts" setup>
import type { AssetRowActionAccess } from '../asset-row-actions';

import type { CategoryNode } from '#/api/base-data';

import { ElButton } from 'element-plus';

defineProps<{
  access: AssetRowActionAccess;
  categoryPath: number[];
  currentCategoryLevel: number;
  currentLevelTitle: string;
  exportingAllAssets: boolean;
  flatMode: boolean;
  hierarchyParent: CategoryNode | null;
  hierarchyTrail: CategoryNode[];
  showAllAssetExport: boolean;
  showAssetTable: boolean;
}>();

const emit = defineEmits<{
  create: [];
  enterFlatMode: [];
  exitFlatMode: [];
  exportAll: [];
  import: [];
  navigate: [index: number];
}>();
</script>

<template>
  <div class="asset-workspace-head">
    <div>
      <div class="asset-section-title">{{ currentLevelTitle }}</div>
      <div v-if="!flatMode || hierarchyParent" class="asset-path">
        <a href="#" @click.prevent="emit('navigate', -1)"> 全部分类 </a>
        <template v-for="(node, index) in hierarchyTrail" :key="node.id">
          <span class="text-muted-foreground">›</span>
          <a href="#" @click.prevent="emit('navigate', index)">
            {{ node.code }}
          </a>
        </template>
      </div>
    </div>
    <div class="asset-head-actions">
      <span class="column-tools-host" data-column-tools="asset-list"></span>
      <ElButton v-if="access.canImport" @click="emit('import')">
        批量导入
      </ElButton>
      <ElButton
        v-if="!flatMode || hierarchyParent"
        @click="emit('enterFlatMode')"
      >
        查看全部资产
      </ElButton>
      <ElButton
        v-if="showAllAssetExport"
        :loading="exportingAllAssets"
        @click="emit('exportAll')"
      >
        导出全部资产
      </ElButton>
      <ElButton v-if="flatMode" @click="emit('exitFlatMode')">
        分类浏览
      </ElButton>
      <ElButton
        v-if="hierarchyParent && !flatMode"
        @click="emit('navigate', categoryPath.length - 2)"
      >
        返回上一层
      </ElButton>
      <ElButton
        v-if="showAssetTable && access.canCreate"
        type="primary"
        @click="emit('create')"
      >
        新增资产
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./AssetWorkspaceHeader.css"></style>
