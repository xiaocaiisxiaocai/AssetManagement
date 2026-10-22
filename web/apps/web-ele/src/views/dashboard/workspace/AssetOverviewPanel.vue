<script lang="ts" setup>
import type { AssetSummary, CategoryStatRow } from '#/api/report';
import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElButton,
  ElCard,
  ElEmpty,
  ElTable,
  ElTableColumn,
} from 'element-plus';

import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { useTableColumns } from '#/components/table/column-visibility';

defineProps<{
  categoryTopRows: CategoryStatRow[];
  summary: AssetSummary | null;
}>();

const emit = defineEmits<{
  categoryClick: [categoryCode: string];
  navigate: [path: null | string];
}>();

const tableColumns: TableColumnOption[] = [
  { key: 'category', label: '分类' },
  { key: 'total', label: '总数' },
  { key: 'available', label: '在库' },
  { key: 'borrowed', label: '借出' },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('dashboard-asset-overview', tableColumns);
</script>

<template>
  <ElCard class="workspace-panel" shadow="never">
    <template #header>
      <div class="workspace-card-header">
        <span>资产概况</span>
        <div class="workspace-card-actions">
        <TableColumnPicker
          :columns="tableColumns"
          :hidden-keys="hiddenKeys"
          @reset="resetColumns"
          @toggle="setColumnVisible"
        />
        <ElButton
          link
          type="primary"
          @click="emit('navigate', '/report/summary')"
        >
          查看汇总
        </ElButton>
        </div>
      </div>
    </template>
    <ElTable
      v-if="summary"
      :data="categoryTopRows"
      border
      class="workspace-table"
    >
      <ElTableColumn
        v-if="visibleColumn('category')"
        label="分类"
        min-width="180"
      >
        <template #default="{ row }">
          <button
            class="category-code-link"
            type="button"
            @click="emit('categoryClick', row.categoryCode)"
          >
            {{ row.categoryCode }}
          </button>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('total')"
        align="center"
        label="总数"
        prop="total"
        width="90"
      />
      <ElTableColumn
        v-if="visibleColumn('available')"
        align="center"
        label="在库"
        prop="available"
        width="90"
      />
      <ElTableColumn
        v-if="visibleColumn('borrowed')"
        align="center"
        label="借出"
        prop="borrowed"
        width="90"
      />
    </ElTable>
    <ElEmpty v-else description="资产概况暂不可用" />
  </ElCard>
</template>

<style scoped src="./workspace-panel.css"></style>
