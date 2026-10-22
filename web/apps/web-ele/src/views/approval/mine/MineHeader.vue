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
    <div class="mine-actions">
      <span class="column-tools-host" data-column-tools="approval-mine"></span>
      <ElButton v-if="canCreate" type="success" @click="emit('start', 'borrow')">
        发起借用
      </ElButton>
      <ElButton
        v-if="canCreate"
        type="warning"
        @click="emit('start', 'transfer')"
      >
        发起转让
      </ElButton>
      <ElButton
        v-if="canCreate"
        type="primary"
        @click="emit('start', 'extension')"
      >
        申请延期
      </ElButton>
      <ElButton v-if="canCreate" @click="emit('start', 'return')">
        发起归还
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./MineHeader.css"></style>
