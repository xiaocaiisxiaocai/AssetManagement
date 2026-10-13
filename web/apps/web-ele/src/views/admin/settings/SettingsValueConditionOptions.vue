<script lang="ts" setup>
import { ElSelect } from 'element-plus';

const options = defineModel<string[]>('options', { required: true });
</script>

<template>
  <ElSelect
    v-model="options"
    allow-create
    default-first-option
    filterable
    multiple
    placeholder="输入选项后按回车添加"
    style="width: 100%"
  />
</template>
