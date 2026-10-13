<script lang="ts" setup>
import { ElInput } from 'element-plus';

const value = defineModel<number | string | undefined>('value', {
  required: true,
});
</script>

<template>
  <ElInput v-model="value" placeholder="请输入参数值" />
</template>
