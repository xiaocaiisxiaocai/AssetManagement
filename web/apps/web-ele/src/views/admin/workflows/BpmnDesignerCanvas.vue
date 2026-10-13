<script lang="ts" setup>
import { ref } from 'vue';

defineProps<{
  loading: boolean;
  selectedElementText: string;
}>();

const containerRef = ref<HTMLDivElement>();

defineExpose({ containerRef });
</script>

<template>
  <main class="canvas-shell">
    <div class="canvas-statusbar">
      <span>当前选中：{{ selectedElementText }}</span>
      <span>网关分支名称会同步显示在画布连线上</span>
    </div>
    <div
      ref="containerRef"
      class="bpmn-container"
      element-loading-text="正在载入流程设计器..."
      v-loading="loading"
    ></div>
  </main>
</template>

<style src="./bpmn-modeler.css"></style>
