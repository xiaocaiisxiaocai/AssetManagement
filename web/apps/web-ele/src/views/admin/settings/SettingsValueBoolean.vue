<script lang="ts" setup>
import { ElRadio, ElRadioGroup } from 'element-plus';

const value = defineModel<number | string | undefined>('value', {
  required: true,
});
</script>

<template>
  <ElRadioGroup v-model="value">
    <ElRadio label="true">启用</ElRadio>
    <ElRadio label="false">禁用</ElRadio>
  </ElRadioGroup>
</template>
