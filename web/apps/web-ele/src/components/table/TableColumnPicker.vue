<script lang="ts" setup>
import type { CheckboxValueType } from 'element-plus';

import type { TableColumnOption } from './column-visibility';

import { ElButton, ElCheckbox, ElPopover } from 'element-plus';

import { isLastVisibleDataColumn } from './column-visibility';

const props = defineProps<{
  columns: readonly TableColumnOption[];
  hiddenKeys: readonly string[];
}>();

const emit = defineEmits<{
  reset: [];
  toggle: [key: string, visible: boolean];
}>();

function shown(column: TableColumnOption) {
  return column.locked || !props.hiddenKeys.includes(column.key);
}

function locked(column: TableColumnOption) {
  return (
    Boolean(column.locked) ||
    (shown(column) &&
      isLastVisibleDataColumn(props.columns, props.hiddenKeys, column.key))
  );
}

function onToggle(column: TableColumnOption, value: CheckboxValueType) {
  if (locked(column)) return;
  emit('toggle', column.key, value === true);
}
</script>

<template>
  <div class="table-column-picker-inline">
    <ElPopover
      :width="248"
      placement="bottom-end"
      popper-class="table-column-picker-popper"
      trigger="click"
    >
      <template #reference>
        <ElButton class="table-column-picker-trigger">列设置</ElButton>
      </template>
      <div class="table-column-picker-list">
        <ElCheckbox
          v-for="column in columns"
          :key="column.key"
          :disabled="locked(column)"
          :model-value="shown(column)"
          @change="onToggle(column, $event)"
        >
          {{ column.label }}
        </ElCheckbox>
      </div>
      <div class="table-column-picker-footer">
        <ElButton link size="small" type="primary" @click="emit('reset')">
          恢复默认
        </ElButton>
      </div>
    </ElPopover>
  </div>
</template>

<style src="./TableColumnPicker.css"></style>
