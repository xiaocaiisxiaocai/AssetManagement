<script lang="ts" setup>
import type { AssetSummary } from '#/api/report';
import type { TableColumnOption } from '#/components/table/column-visibility';

import { ElTable, ElTableColumn } from 'element-plus';

import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { useTableColumns } from '#/components/table/column-visibility';

defineProps<{
  loading: boolean;
  summary: AssetSummary;
}>();

const emit = defineEmits<{
  categoryClick: [categoryCode: string];
}>();

const categoryColumns: TableColumnOption[] = [
  { key: 'category', label: '分类' },
  { key: 'total', label: '总数' },
  { key: 'available', label: '在库' },
  { key: 'borrowed', label: '借出' },
  { key: 'percent', label: '占比' },
];
const departmentColumns: TableColumnOption[] = [
  { key: 'department', label: '部门' },
  { key: 'total', label: '总数' },
  { key: 'available', label: '在库' },
  { key: 'borrowed', label: '借出' },
  { key: 'percent', label: '占比' },
];
const {
  hiddenKeys: categoryHiddenKeys,
  resetColumns: resetCategoryColumns,
  setColumnVisible: setCategoryColumnVisible,
  visibleColumn: categoryColumnVisible,
} = useTableColumns('report-summary-category', categoryColumns);
const {
  hiddenKeys: departmentHiddenKeys,
  resetColumns: resetDepartmentColumns,
  setColumnVisible: setDepartmentColumnVisible,
  visibleColumn: departmentColumnVisible,
} = useTableColumns('report-summary-dept', departmentColumns);
</script>

<template>
  <div class="summary-tables page-fill">
    <div class="summary-table-panel">
      <div class="summary-table-title">
        <span>按分类统计</span>
        <TableColumnPicker
          :columns="categoryColumns"
          :hidden-keys="categoryHiddenKeys"
          @reset="resetCategoryColumns"
          @toggle="setCategoryColumnVisible"
        />
      </div>
      <ElTable :data="summary.byCategory" border v-loading="loading">
        <ElTableColumn
          v-if="categoryColumnVisible('category')"
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
          v-if="categoryColumnVisible('total')"
          align="center"
          label="总数"
          prop="total"
          width="100"
        />
        <ElTableColumn
          v-if="categoryColumnVisible('available')"
          align="center"
          label="在库"
          prop="available"
          width="100"
        />
        <ElTableColumn
          v-if="categoryColumnVisible('borrowed')"
          align="center"
          class-name="hide-on-mobile"
          label="借出"
          prop="borrowed"
          width="100"
        />
        <ElTableColumn
          v-if="categoryColumnVisible('percent')"
          align="center"
          class-name="hide-on-mobile"
          label="占比"
          width="100"
        >
          <template #default="{ row }">{{ row.percent }}%</template>
        </ElTableColumn>
      </ElTable>
    </div>

    <div class="summary-table-panel">
      <div class="summary-table-title">
        <span>按部门统计</span>
        <TableColumnPicker
          :columns="departmentColumns"
          :hidden-keys="departmentHiddenKeys"
          @reset="resetDepartmentColumns"
          @toggle="setDepartmentColumnVisible"
        />
      </div>
      <ElTable :data="summary.byDept" border v-loading="loading">
        <ElTableColumn
          v-if="departmentColumnVisible('department')"
          label="部门"
          min-width="180"
        >
          <template #default="{ row }">
            <div>{{ row.departmentName }}</div>
          </template>
        </ElTableColumn>
        <ElTableColumn
          v-if="departmentColumnVisible('total')"
          align="center"
          label="总数"
          prop="total"
          width="100"
        />
        <ElTableColumn
          v-if="departmentColumnVisible('available')"
          align="center"
          label="在库"
          prop="available"
          width="100"
        />
        <ElTableColumn
          v-if="departmentColumnVisible('borrowed')"
          align="center"
          class-name="hide-on-mobile"
          label="借出"
          prop="borrowed"
          width="100"
        />
        <ElTableColumn
          v-if="departmentColumnVisible('percent')"
          align="center"
          class-name="hide-on-mobile"
          label="占比"
          width="100"
        >
          <template #default="{ row }">{{ row.percent }}%</template>
        </ElTableColumn>
      </ElTable>
    </div>
  </div>
</template>

<style scoped src="./SummaryTables.css"></style>
