<script lang="ts" setup>
import { ElButton } from 'element-plus';

defineProps<{
  canCreate: boolean;
}>();

const emit = defineEmits<{
  start: [type: string];
}>();
</script>

<template>
  <div class="mine-header">
    <div>
      <h2 class="mine-title">我的申请</h2>
    </div>
    <div v-if="canCreate" class="mine-actions">
      <ElButton type="success" @click="emit('start', 'borrow')">
        发起借用
      </ElButton>
      <ElButton type="warning" @click="emit('start', 'transfer')">
        发起转让
      </ElButton>
      <ElButton type="primary" @click="emit('start', 'extension')">
        申请延期
      </ElButton>
      <ElButton @click="emit('start', 'return')">发起归还</ElButton>
    </div>
  </div>
</template>

<style scoped src="./MineHeader.css"></style>
