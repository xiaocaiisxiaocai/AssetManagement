<script lang="ts" setup>
import type { AssetDetail } from '#/api/asset';
import type { TableColumnOption } from '#/components/table/column-visibility';

import { ElEmpty, ElTable, ElTableColumn, ElTag } from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { formatDateTime } from '#/utils/date-format';

defineProps<{
  actionTag: (
    action: null | string | undefined,
  ) => 'danger' | 'info' | 'primary' | 'success' | 'warning';
  actionText: (action: null | string | undefined) => string;
  detail: AssetDetail;
  summaryText: (summary: null | string | undefined) => string;
}>();

const tableColumns: TableColumnOption[] = [
  { key: 'time', label: '时间' },
  { key: 'user', label: '操作人' },
  { key: 'action', label: '动作' },
  { key: 'summary', label: '摘要' },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('asset-detail-logs', tableColumns);
</script>

<template>
  <div class="ad-tab-content">
    <section class="ad-section">
      <Teleport defer to="[data-column-tools='asset-detail-logs']">
        <TableColumnPicker
          :columns="tableColumns"
          :hidden-keys="hiddenKeys"
          @reset="resetColumns"
          @toggle="setColumnVisible"
        />
      </Teleport>
      <ElTable
        v-if="detail.recentLogs.length > 0"
        :data="detail.recentLogs"
        border
        max-height="480"
        size="small"
        stripe
      >
        <ElTableColumn v-if="visibleColumn('time')" label="时间" width="170">
          <template #default="{ row }">
            {{
              formatDateTime(row.occurredAt, {
                empty: '—',
                seconds: true,
              })
            }}
          </template>
        </ElTableColumn>
        <ElTableColumn
          v-if="visibleColumn('user')"
          label="操作人"
          prop="userName"
          width="110"
        />
        <ElTableColumn v-if="visibleColumn('action')" label="动作" width="90">
          <template #default="{ row }">
            <ElTag
              :type="actionTag(row.actionType)"
              effect="light"
              size="small"
            >
              {{ actionText(row.actionType) }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn
          v-if="visibleColumn('summary')"
          label="摘要"
          show-overflow-tooltip
        >
          <template #default="{ row }">
            <span class="ad-log-summary">{{ summaryText(row.summary) }}</span>
          </template>
        </ElTableColumn>
      </ElTable>
      <ElEmpty v-else :image-size="56" description="暂无操作日志" />
    </section>
  </div>
</template>

<style scoped src="./AssetDetailDialog.css"></style>
