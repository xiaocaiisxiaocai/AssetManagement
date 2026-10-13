<script lang="ts" setup>
import type { MaterialDetail } from '#/api/material';

import { ElEmpty, ElTimeline, ElTimelineItem } from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

import { materialRecordActionText } from './material-records';

defineProps<{
  detail: MaterialDetail;
}>();
</script>

<template>
  <section class="material-flow-section">
    <div class="material-flow-title">流转记录</div>
    <ElTimeline v-if="detail.records.length > 0" class="material-flow-timeline">
      <ElTimelineItem
        v-for="record in detail.records"
        :key="record.key"
        :timestamp="formatDateTime(record.operatedAt, { seconds: true })"
      >
        <div class="material-flow-record">
          <span class="font-medium">
            {{ materialRecordActionText(record.action) }}
          </span>
          <span v-if="record.operator" class="ml-2 text-gray-500">
            {{ record.operator }}
          </span>
          <div v-if="record.comment" class="material-flow-comment">
            {{ record.comment }}
          </div>
        </div>
      </ElTimelineItem>
    </ElTimeline>
    <ElEmpty v-else :image-size="60" description="暂无流转记录" />
  </section>
</template>

<style scoped src="./MaterialDetailDialog.css"></style>
