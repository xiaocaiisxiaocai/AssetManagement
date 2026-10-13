<script lang="ts" setup>
import type { AssetDetail } from '#/api/asset';

import { ElEmpty, ElTable, ElTableColumn, ElTag } from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

defineProps<{
  actionTag: (
    action: null | string | undefined,
  ) => 'danger' | 'info' | 'primary' | 'success' | 'warning';
  actionText: (action: null | string | undefined) => string;
  detail: AssetDetail;
  summaryText: (summary: null | string | undefined) => string;
}>();
</script>

<template>
  <div class="ad-tab-content">
    <section class="ad-section">
      <ElTable
        v-if="detail.recentLogs.length > 0"
        :data="detail.recentLogs"
        border
        max-height="480"
        size="small"
        stripe
      >
        <ElTableColumn label="时间" width="170">
          <template #default="{ row }">
            {{
              formatDateTime(row.occurredAt, {
                empty: '—',
                seconds: true,
              })
            }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作人" prop="userName" width="110" />
        <ElTableColumn label="动作" width="90">
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
        <ElTableColumn label="摘要" show-overflow-tooltip>
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
