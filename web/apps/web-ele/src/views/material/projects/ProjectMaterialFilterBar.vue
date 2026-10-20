<script lang="ts" setup>
import { ElButton } from 'element-plus';

defineProps<{
  canCreate: boolean;
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
  </div>
</template>

<style scoped src="./ProjectMaterialsTab.css"></style>
