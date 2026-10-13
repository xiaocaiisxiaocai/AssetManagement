<script lang="ts" setup>
import { ElButton } from 'element-plus';

defineProps<{
  backupLoading: boolean;
  canManage: boolean;
}>();

const emit = defineEmits<{
  backup: [];
}>();
</script>

<template>
  <div class="backup-header">
    <div>
      <h2 class="backup-title">数据库备份</h2>
    </div>
    <div class="backup-actions">
      <ElButton
        v-if="canManage"
        :loading="backupLoading"
        type="primary"
        @click="emit('backup')"
      >
        生成完整备份包
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./BackupHeader.css"></style>
