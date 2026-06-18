<script lang="ts" setup>
import type { AssetDetail, AssetStatus } from '#/api/asset';

import {
  ElDialog,
  ElImage,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

import { assetImageUrl } from '#/api/asset';

defineProps<{
  detail: AssetDetail | null;
  loading: boolean;
}>();

const visible = defineModel<boolean>('visible', { default: false });

const statusOptions: Array<{
  label: string;
  tag: 'danger' | 'info' | 'success' | 'warning';
  value: AssetStatus;
}> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
  { label: '维修', tag: 'info', value: 2 },
  { label: '报废', tag: 'danger', value: 3 },
];

const bizTypeText: Record<string, string> = {
  borrow: '借用',
  return: '归还',
  scrap: '报废',
  transfer: '转让',
};

const flowStatusMeta: Record<
  string,
  { tag: 'danger' | 'info' | 'success' | 'warning'; text: string }
> = {
  approved: { tag: 'success', text: '已通过' },
  pending: { tag: 'warning', text: '审批中' },
  rejected: { tag: 'danger', text: '已驳回' },
};

function flowTitle(flow: AssetDetail['flows'][number]) {
  const biz = bizTypeText[flow.bizType] ?? flow.bizType;
  const status = flowStatusMeta[flow.status]?.text ?? flow.status;
  return `${biz} · ${status}`;
}

function formatTime(time: null | string) {
  if (!time) {
    return '—';
  }
  const d = new Date(time);
  return Number.isNaN(d.getTime())
    ? time
    : d.toLocaleString('zh-CN', { hour12: false });
}
</script>

<template>
  <ElDialog v-model="visible" title="资产详情" width="720px">
    <div v-loading="loading" class="space-y-5">
      <template v-if="detail">
        <section>
          <div class="mb-2 font-medium">基本信息</div>
          <div class="grid grid-cols-2 gap-x-6 gap-y-1 text-sm">
            <div>资产编号：{{ detail.asset.assetNo }}</div>
            <div>名称：{{ detail.asset.name }}</div>
            <div>分类：{{ detail.asset.categoryName }}</div>
            <div>
              状态：
              <ElTag :type="statusOptions[detail.asset.status]?.tag" size="small">
                {{ statusOptions[detail.asset.status]?.label }}
              </ElTag>
            </div>
            <div>归属部门：{{ detail.asset.departmentName ?? '—' }}</div>
            <div>存放位置：{{ detail.asset.locationName ?? '—' }}</div>
            <div>保管人：{{ detail.asset.custodianName ?? '—' }}</div>
            <div>
              型号品牌：{{ detail.asset.model || '—' }} /
              {{ detail.asset.brand || '—' }}
            </div>
            <div>单价：{{ detail.asset.price }}</div>
            <div>数量：{{ detail.asset.quantity }}</div>
          </div>
        </section>

        <section v-if="detail.asset.images && detail.asset.images.length">
          <div class="mb-2 font-medium">资产照片</div>
          <div class="flex flex-wrap gap-2">
            <ElImage
              v-for="(url, i) in detail.asset.images"
              :key="i"
              :initial-index="i"
              :preview-src-list="(detail.asset.images || []).map(assetImageUrl)"
              :src="assetImageUrl(url)"
              class="h-20 w-20 rounded border"
              fit="cover"
            />
          </div>
        </section>

        <section>
          <div class="mb-2 font-medium">流转时间线</div>
          <ElTimeline v-if="detail.flows.length">
            <ElTimelineItem
              v-for="flow in detail.flows"
              :key="flow.id"
              :timestamp="formatTime(flow.applyTime)"
              :type="flowStatusMeta[flow.status]?.tag ?? 'primary'"
            >
              <div class="text-sm">
                <span class="font-medium">{{ flowTitle(flow) }}</span>
                <span class="text-gray-500"> · {{ flow.applicant }}</span>
                <span v-if="flow.transferee" class="text-gray-500">
                  → {{ flow.transferee }}</span>
              </div>
              <div v-if="flow.reason" class="text-xs text-gray-400">
                事由：{{ flow.reason }}
              </div>
              <div v-if="flow.returnDate" class="text-xs text-gray-400">
                应归还：{{ flow.returnDate }}
              </div>
            </ElTimelineItem>
          </ElTimeline>
          <div v-else class="text-sm text-gray-400">暂无流转记录</div>
        </section>

        <section>
          <div class="mb-2 font-medium">最近操作日志</div>
          <ElTable
            v-if="detail.recentLogs.length"
            :data="detail.recentLogs"
            size="small"
          >
            <ElTableColumn label="时间" width="170">
              <template #default="{ row }">{{ formatTime(row.occurredAt) }}</template>
            </ElTableColumn>
            <ElTableColumn label="操作人" prop="userName" width="110" />
            <ElTableColumn label="动作" prop="actionType" width="90" />
            <ElTableColumn label="摘要" prop="summary" show-overflow-tooltip />
          </ElTable>
          <div v-else class="text-sm text-gray-400">暂无操作日志</div>
        </section>
      </template>
    </div>
  </ElDialog>
</template>
