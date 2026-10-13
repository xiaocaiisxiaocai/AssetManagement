<script lang="ts" setup>
import { ElInputNumber } from 'element-plus';

defineProps<{
  max?: number;
  min?: number;
}>();

const value = defineModel<number | undefined>('value', { required: true });
</script>

<template>
  <ElInputNumber
    v-model="value"
    :max="max"
    :min="min"
    :step="1"
    :step-strictly="true"
    style="width: 100%"
  />
</template>
