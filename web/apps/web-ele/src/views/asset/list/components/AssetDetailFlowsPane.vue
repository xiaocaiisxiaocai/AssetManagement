<script lang="ts" setup>
import type { AssetDetail } from '#/api/asset';

import { ElTag, ElTimeline, ElTimelineItem } from 'element-plus';

import { formatDate, formatDateTime } from '#/utils/date-format';

import { flowParticipantText } from './asset-detail-timeline';

defineProps<{
  detail: AssetDetail;
  flowStatusMeta: Record<
    string,
    { tag: 'danger' | 'info' | 'primary' | 'success' | 'warning' }
  >;
  flowTitle: (flow: AssetDetail['flows'][number]) => string;
}>();
</script>

<template>
  <div class="ad-tab-content">
    <section class="ad-section ad-timeline-section">
      <ElTimeline>
        <ElTimelineItem
          v-for="flow in detail.flows"
          :key="flow.id"
          :timestamp="
            formatDateTime(
              flow.status === 'withdrawn' ? flow.withdrawnAt : flow.applyTime,
            )
          "
          :type="flowStatusMeta[flow.status]?.tag ?? 'primary'"
        >
          <div class="text-sm">
            <span class="font-medium">{{ flowTitle(flow) }}</span>
            <span class="text-gray-500">
              · {{ flowParticipantText(flow) }}
            </span>
          </div>
          <div v-if="flow.reason" class="text-xs text-gray-400">
            事由：{{ flow.reason }}
          </div>
          <div
            v-if="flow.bizType === 'extension'"
            class="text-xs text-gray-400"
          >
            期限：{{ formatDate(flow.originalReturnDate) }} →
            {{ formatDate(flow.returnDate) }}
          </div>
          <div v-else-if="flow.returnDate" class="text-xs text-gray-400">
            应归还：{{ formatDate(flow.returnDate) }}
          </div>
        </ElTimelineItem>
        <ElTimelineItem
          :timestamp="formatDateTime(detail.asset.createdAt, { empty: '—' })"
          hollow
          type="primary"
        >
          <div class="ad-initial-title">
            <span class="font-medium">资产登记 · 初始保管</span>
            <ElTag effect="plain" size="small" type="primary">起点</ElTag>
          </div>
          <div class="ad-initial-custodian">
            初始保管人：{{ detail.initialCustodianName ?? '未指定' }}
          </div>
        </ElTimelineItem>
      </ElTimeline>
    </section>
  </div>
</template>

<style scoped src="./AssetDetailDialog.css"></style>
