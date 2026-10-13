<script lang="ts" setup>
import { ElButton } from 'element-plus';

defineProps<{
  canCleanup: boolean;
}>();

const emit = defineEmits<{
  cleanup: [];
}>();
</script>

<template>
  <div class="page-header">
    <div>
      <h2 class="page-title">审计日志</h2>
    </div>
    <div class="header-actions">
      <ElButton v-if="canCleanup" type="warning" @click="emit('cleanup')">
        清理日志
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./AuditHeader.css"></style>
