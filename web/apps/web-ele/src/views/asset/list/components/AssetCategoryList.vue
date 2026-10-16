<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';

import {
  ElButton,
  ElInput,
  ElOption,
  ElPagination,
  ElSelect,
} from 'element-plus';

import { countCategoryTreeAssets } from '../category-asset-counts';

const props = defineProps<{
  categoryAssetCounts: Record<string, number>;
  currentCategoryLevel: number;
  filteredHierarchyNodes: CategoryNode[];
  maxCategoryLevel: number;
  nextLevelName: string;
  pagedHierarchyNodes: CategoryNode[];
  pageSizeOptions: number[];
}>();

const emit = defineEmits<{
  enter: [node: CategoryNode];
  reset: [];
}>();

const hierarchyKeyword = defineModel<string>('hierarchyKeyword', {
  required: true,
});
const categoryPage = defineModel<number>('categoryPage', { required: true });
const categoryPageSize = defineModel<number>('categoryPageSize', {
  required: true,
});

function countCategoryAssets(node: CategoryNode) {
  return countCategoryTreeAssets(node, props.categoryAssetCounts);
}

function categoryChildLabel() {
  const level = props.currentCategoryLevel + 2;
  if (level === 2) return '二级分类';
  if (level === 3) return '三级分类';
  return '下级分类';
}
</script>

<template>
  <div class="asset-filter-strip">
    <label>关键字搜索</label>
    <ElInput
      v-model="hierarchyKeyword"
      clearable
      placeholder="按编码/备注搜索"
      style="width: 240px"
      @input="categoryPage = 1"
    />
    <ElButton @click="emit('reset')">重置</ElButton>
  </div>

  <div v-if="filteredHierarchyNodes.length > 0" class="asset-class-list table-scroll">
    <article
      v-for="node in pagedHierarchyNodes"
      :key="node.id"
      class="asset-class-row"
      tabindex="0"
      @click="emit('enter', node)"
      @keydown.enter.prevent="emit('enter', node)"
    >
      <div class="asset-class-code">
        <span>{{ node.codeSeg || node.code }}</span>
      </div>
      <div class="asset-class-main">
        <div class="asset-class-name">
          {{ node.remark || '无备注' }}
        </div>
      </div>
      <div class="asset-class-actions" @click.stop>
        <span
          v-if="currentCategoryLevel === maxCategoryLevel - 1"
          class="asset-row-warning"
        >
          有 {{ countCategoryAssets(node) }} 条资产记录
        </span>
        <span v-else class="asset-row-warning">
          有 {{ node.children.length }} 个{{ categoryChildLabel() }}
        </span>
        <ElButton
          class="asset-enter-button"
          link
          type="primary"
          @click.stop="emit('enter', node)"
        >
          进入
        </ElButton>
      </div>
    </article>
  </div>
  <div v-else class="asset-empty">
    当前分类下暂无{{ nextLevelName }}，请在“资产分类”页面维护分类。
  </div>

  <div v-if="filteredHierarchyNodes.length > 0" class="asset-pager">
    <div class="asset-pager-left">
      <span>共 {{ filteredHierarchyNodes.length }} 条记录</span>
      <span class="asset-pager-divider">|</span>
      <span>每页</span>
      <ElSelect
        v-model="categoryPageSize"
        style="width: 92px"
        @change="categoryPage = 1"
      >
        <ElOption
          v-for="size in pageSizeOptions"
          :key="size"
          :label="`${size}`"
          :value="size"
        />
      </ElSelect>
    </div>
    <ElPagination
      v-model:current-page="categoryPage"
      :page-size="categoryPageSize"
      :total="filteredHierarchyNodes.length"
      background
      layout="prev, pager, next"
    />
  </div>
</template>

<style scoped src="./AssetCategoryList.css"></style>
