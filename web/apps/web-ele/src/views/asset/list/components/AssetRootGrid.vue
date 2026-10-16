<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';

import { ElButton } from 'element-plus';

defineProps<{
  hierarchyNodes: CategoryNode[];
}>();

const emit = defineEmits<{
  enter: [node: CategoryNode];
}>();
</script>

<template>
  <div v-if="hierarchyNodes.length > 0" class="asset-root-grid table-scroll">
    <article
      v-for="node in hierarchyNodes"
      :key="node.id"
      class="asset-root-card"
      tabindex="0"
      @click="emit('enter', node)"
      @keydown.enter.prevent="emit('enter', node)"
    >
      <div class="asset-root-card-code">
        <span>{{ node.codeSeg || node.code }}</span>
      </div>
      <div class="asset-root-card-body">
        <div class="asset-row-warning">
          有 {{ node.children.length }} 个二级分类
        </div>
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
    暂无一级分类，请在“资产分类”页面维护分类。
  </div>
</template>

<style scoped src="./AssetRootGrid.css"></style>
