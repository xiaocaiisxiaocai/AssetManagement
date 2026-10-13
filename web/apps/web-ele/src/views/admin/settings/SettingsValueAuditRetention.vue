<script lang="ts" setup>
import { ElOption, ElSelect } from 'element-plus';

defineProps<{
  options: Set<number>;
}>();

const value = defineModel<number | string | undefined>('value', {
  required: true,
});
</script>

<template>
  <ElSelect v-model="value" placeholder="请选择保留天数" style="width: 100%">
    <ElOption
      v-for="days in options"
      :key="days"
      :label="`${days} 天`"
      :value="days"
    />
  </ElSelect>
</template>
