<script lang="ts" setup>
import type { OverdueReportRow } from '#/api/report';

import {
  ElButton,
  ElCard,
  ElEmpty,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineProps<{
  overdueRows: null | OverdueReportRow[];
}>();

const emit = defineEmits<{
  navigate: [path: null | string];
}>();
</script>

<template>
  <ElCard class="workspace-panel" shadow="never">
    <template #header>
      <div class="workspace-card-header">
        <span>逾期资产</span>
        <div class="workspace-card-actions">
          <ElButton
            link
            type="primary"
            @click="emit('navigate', '/report/overdue')"
          >
            查看全部
          </ElButton>
        </div>
      </div>
    </template>
    <ElTable
      v-if="overdueRows && overdueRows.length > 0"
      :data="overdueRows.slice(0, 5)"
      border
      class="workspace-table workspace-overdue-table"
    >
      <ElTableColumn align="center" label="资产" min-width="180">
        <template #default="{ row }">
          <div class="workspace-overdue-asset">
            <div class="workspace-overdue-name">{{ row.assetName }}</div>
            <ElTag class="workspace-overdue-no" size="small">
              {{ row.assetNo }}
            </ElTag>
          </div>
        </template>
      </ElTableColumn>
      <ElTableColumn label="借用人" prop="borrower" width="96" />
      <ElTableColumn align="center" label="逾期天数" width="104">
        <template #default="{ row }">
          <ElTag :type="row.isSerious ? 'danger' : 'warning'" size="small">
            {{ row.overdueDays }} 天
          </ElTag>
        </template>
      </ElTableColumn>
    </ElTable>
    <ElEmpty v-else-if="overdueRows" description="暂无逾期资产" />
    <ElEmpty v-else description="逾期数据暂不可用" />
  </ElCard>
</template>

<style scoped src="./workspace-panel.css"></style>
<style scoped>
.workspace-overdue-asset {
  display: flex;
  flex-direction: column;
  gap: 4px;
  align-items: center;
  min-width: 0;
  text-align: center;
}

.workspace-overdue-name {
  width: 100%;
  line-height: 20px;
  overflow-wrap: anywhere;
}

.workspace-overdue-no {
  max-width: 100%;
}
</style>
