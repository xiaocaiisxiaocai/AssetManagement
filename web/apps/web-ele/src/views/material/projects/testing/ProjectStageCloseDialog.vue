<script lang="ts" setup>
import { ElButton, ElDatePicker, ElDialog } from 'element-plus';

import { calendarDateText } from '#/utils/business-date';

const props = defineProps<{
  maxDate: string;
  minDate: string;
  name: string;
  sequence: number;
}>();

const emit = defineEmits<{ confirm: [] }>();
const visible = defineModel<boolean>('visible', { required: true });
const endDate = defineModel<string>('endDate', { required: true });

function disableOutOfRange(date: Date) {
  const text = calendarDateText(date);
  return text < props.minDate || text > props.maxDate;
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="false"
    :title="`结束第 ${sequence} 阶段`"
    append-to-body
    width="420px"
  >
    <div class="stage-close-row">
      <span class="stage-close-name">{{ name }}</span>
      <ElDatePicker
        v-model="endDate"
        :clearable="false"
        :disabled-date="disableOutOfRange"
        class="stage-close-date"
        placeholder="选择日期"
        type="date"
        value-format="YYYY-MM-DD"
      />
    </div>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton type="primary" @click="emit('confirm')">结束并保存</ElButton>
    </template>
  </ElDialog>
</template>

<style scoped>
.stage-close-row {
  display: flex;
  gap: 12px;
  align-items: center;
}

.stage-close-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stage-close-date {
  flex: none;
  width: 160px;
}
</style>
