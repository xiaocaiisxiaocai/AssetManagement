<script lang="ts" setup>
import { ElTimePicker } from 'element-plus';

const value = defineModel<number | string | undefined>('value', {
  required: true,
});
</script>

<template>
  <ElTimePicker
    v-model="value"
    format="HH:mm"
    placeholder="请选择时间"
    value-format="HH:mm"
  />
</template>
