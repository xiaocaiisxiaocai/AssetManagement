<script lang="ts" setup>
import { ElButton, ElButtonGroup } from 'element-plus';

defineProps<{
  loading: boolean;
  savePending: boolean;
}>();

const emit = defineEmits<{
  download: [];
  save: [];
  zoomIn: [];
  zoomOut: [];
  zoomReset: [];
}>();
</script>

<template>
  <div class="designer-toolbar">
    <div class="toolbar-brand">
      <span class="brand-mark">流程</span>
      <div class="brand-title">审批流程设计</div>
    </div>
    <div class="toolbar-actions">
      <ElButtonGroup>
        <ElButton @click="emit('zoomOut')">缩小</ElButton>
        <ElButton @click="emit('zoomReset')">适应</ElButton>
        <ElButton @click="emit('zoomIn')">放大</ElButton>
      </ElButtonGroup>
      <ElButton @click="emit('download')"> 下载 </ElButton>
      <ElButton
        :disabled="loading || savePending"
        :loading="savePending"
        type="primary"
        @click="emit('save')"
      >
        保存
      </ElButton>
    </div>
  </div>
</template>
