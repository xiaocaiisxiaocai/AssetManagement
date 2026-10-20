<script lang="ts" setup>
import { ElButton } from 'element-plus';

defineProps<{
  canCreate: boolean;
  readOnlyHint?: string;
}>();

const emit = defineEmits<{
  create: [];
}>();
</script>

<template>
  <div class="material-filter">
    <ElButton v-if="canCreate" type="primary" @click="emit('create')">
      新增料件
    </ElButton>
    <span v-else-if="readOnlyHint" class="material-read-only-hint">
      {{ readOnlyHint }}
    </span>
  </div>
</template>

<style scoped src="./ProjectMaterialsTab.css"></style>
