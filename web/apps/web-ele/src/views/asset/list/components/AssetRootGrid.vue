<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';

defineProps<{
  hierarchyNodes: CategoryNode[];
}>();

const emit = defineEmits<{
  enter: [node: CategoryNode];
}>();
</script>

<template>
  <div v-if="hierarchyNodes.length > 0" class="asset-root-grid table-scroll">
    <button
      v-for="node in hierarchyNodes"
      :key="node.id"
      class="asset-root-card"
      type="button"
      @click="emit('enter', node)"
    >
      <div class="asset-root-card-code">
        <span>{{ node.codeSeg || node.code }}</span>
      </div>
      <div class="asset-root-card-body">
        <div class="asset-row-warning">
          有 {{ node.children.length }} 个二级分类
        </div>
        <span class="asset-enter-button">进入</span>
      </div>
    </button>
  </div>
  <div v-else class="asset-empty">
    暂无一级分类，请在“资产分类”页面维护分类。
  </div>
</template>

<style scoped src="./AssetRootGrid.css"></style>
