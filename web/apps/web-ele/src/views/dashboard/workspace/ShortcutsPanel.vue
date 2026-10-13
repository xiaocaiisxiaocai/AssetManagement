<script lang="ts" setup>
import { ElCard } from 'element-plus';

defineProps<{
  shortcuts: { label: string; path: null | string }[];
}>();

const emit = defineEmits<{
  navigate: [path: null | string];
}>();
</script>

<template>
  <ElCard class="workspace-panel" shadow="never">
    <template #header>
      <div class="workspace-card-header">
        <span>快捷入口</span>
      </div>
    </template>
    <div class="workspace-shortcuts">
      <button
        v-for="item in shortcuts"
        :key="item.label"
        class="workspace-shortcut-item"
        type="button"
        @click="emit('navigate', item.path)"
      >
        {{ item.label }}
      </button>
    </div>
  </ElCard>
</template>

<style scoped src="./workspace-panel.css"></style>
<style scoped src="./ShortcutsPanel.css"></style>
