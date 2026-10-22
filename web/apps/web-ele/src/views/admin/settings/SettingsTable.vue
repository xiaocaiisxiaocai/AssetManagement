<script lang="ts" setup>
import type { SystemSetting } from '#/api/base-data';
import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
} from 'element-plus';

import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { useTableColumns } from '#/components/table/column-visibility';

defineProps<{
  canEdit: boolean;
  loading: boolean;
  pageSizeOptions: number[];
  pagedSettings: SystemSetting[];
  total: number;
}>();

const emit = defineEmits<{
  edit: [row: SystemSetting];
  pageSizeChange: [];
}>();
const page = defineModel<number>('page', { required: true });
const pageSize = defineModel<number>('pageSize', { required: true });

function parseStringList(value: string) {
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed)
      ? parsed.filter((item): item is string => typeof item === 'string')
      : [];
  } catch {
    return [];
  }
}

function displaySettingValue(row: SystemSetting) {
  return row.key === 'asset_condition_options'
    ? parseStringList(row.value).join('、')
    : row.value;
}

const tableColumns: TableColumnOption[] = [
  { key: 'key', label: '参数键' },
  { key: 'value', label: '参数值' },
  { key: 'description', label: '说明' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('admin-settings', tableColumns);
</script>

<template>
  <div class="table-panel page-fill table-scroll">
    <ElTable :data="pagedSettings" border height="100%" v-loading="loading">
      <ElTableColumn
        v-if="visibleColumn('key')"
        label="参数键"
        min-width="200"
        prop="key"
      />
      <ElTableColumn v-if="visibleColumn('value')" label="参数值" min-width="180">
        <template #default="{ row }">
          {{ displaySettingValue(row) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('description')"
        class-name="hide-on-mobile"
        label="说明"
        min-width="260"
        prop="description"
      />
      <ElTableColumn
        v-if="canEdit && visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="160"
      >
        <template #default="{ row }">
          <ElButton link size="small" type="primary" @click="emit('edit', row)">
            编辑
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <TableColumnPicker
          :columns="tableColumns"
          :hidden-keys="hiddenKeys"
          @reset="resetColumns"
          @toggle="setColumnVisible"
        />
        <span>共 {{ total }} 条</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="pageSize"
          style="width: 92px"
          @change="emit('pageSizeChange')"
        >
          <ElOption
            v-for="size in pageSizeOptions"
            :key="size"
            :label="`${size} 条`"
            :value="size"
          />
        </ElSelect>
      </div>
      <ElPagination
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        background
        layout="prev, pager, next, jumper"
      />
    </div>
  </div>
</template>
