<script lang="ts" setup>
import type { AssetDetail, AssetStatus } from '#/api/asset';

import {
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElEmpty,
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
];

const bizTypeText: Record<string, string> = {
  borrow: '借用',
  return: '归还',
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

function statusMeta(status: AssetStatus) {
  return (
    statusOptions.find((item) => item.value === status) ?? {
      label: '未知',
      tag: 'info' as const,
      value: status,
    }
  );
}

function formatTime(time: null | string | undefined) {
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
  <ElDialog
    v-model="visible"
    class="asset-detail-dialog"
    title="资产详情"
    width="760px"
  >
    <div v-loading="loading" class="asset-detail">
      <template v-if="detail">
        <header class="ad-header">
          <div class="ad-header-main">
            <h3 class="ad-title">{{ detail.asset.name }}</h3>
            <div class="ad-sub">
              <span class="ad-no">{{ detail.asset.assetNo }}</span>
              <span class="ad-dot">·</span>
              <span>{{ detail.asset.categoryCode }}</span>
            </div>
          </div>
          <div class="ad-tags">
            <ElTag
              :type="statusMeta(detail.asset.status).tag"
              effect="dark"
              size="large"
            >
              {{ statusMeta(detail.asset.status).label }}
            </ElTag>
            <ElTag
              v-if="detail.asset.isDeleted"
              effect="plain"
              size="large"
              type="danger"
            >
              已删除
            </ElTag>
          </div>
        </header>

        <div v-if="detail.asset.isDeleted" class="ad-deleted-banner">
          该资产已于 {{ formatTime(detail.asset.deletedAt) }} 删除，可由有权限的人员在列表中「撤销删除」恢复或「彻底删除」。
        </div>

        <ElDescriptions :column="2" border class="ad-desc" size="small">
          <ElDescriptionsItem label="归属部门">
            {{ detail.asset.departmentName ?? '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="存放位置">
            {{ detail.asset.locationName ?? '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="保管人">
            {{ detail.asset.custodianName ?? '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="数量">
            {{ detail.asset.quantity }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="型号">
            {{ detail.asset.model || '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="品牌">
            {{ detail.asset.brand || '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="创建时间">
            {{ formatTime(detail.asset.createdAt) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="detail.asset.isDeleted" label="删除时间">
            {{ formatTime(detail.asset.deletedAt) }}
          </ElDescriptionsItem>
        </ElDescriptions>

        <section
          v-if="detail.asset.images && detail.asset.images.length"
          class="ad-section"
        >
          <div class="ad-section-title">资产照片</div>
          <div class="ad-photos">
            <ElImage
              v-for="(url, i) in detail.asset.images"
              :key="i"
              :initial-index="i"
              :preview-src-list="(detail.asset.images || []).map(assetImageUrl)"
              :src="assetImageUrl(url)"
              class="ad-photo"
              fit="cover"
              preview-teleported
            />
          </div>
        </section>

        <section class="ad-section">
          <div class="ad-section-title">流转时间线</div>
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
          <ElEmpty v-else :image-size="56" description="暂无流转记录" />
        </section>

        <section class="ad-section">
          <div class="ad-section-title">最近操作日志</div>
          <ElTable
            v-if="detail.recentLogs.length"
            :data="detail.recentLogs"
            size="small"
          >
            <ElTableColumn label="时间" width="170">
              <template #default="{ row }">
                {{ formatTime(row.occurredAt) }}
              </template>
            </ElTableColumn>
            <ElTableColumn label="操作人" prop="userName" width="110" />
            <ElTableColumn label="动作" prop="actionType" width="90" />
            <ElTableColumn label="摘要" prop="summary" show-overflow-tooltip />
          </ElTable>
          <ElEmpty v-else :image-size="56" description="暂无操作日志" />
        </section>
      </template>
      <ElEmpty v-else-if="!loading" description="暂无数据" />
    </div>
  </ElDialog>
</template>

<style scoped>
.asset-detail {
  min-height: 120px;
}

.ad-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 16px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.ad-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 1.4;
  color: var(--el-text-color-primary);
}

.ad-sub {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 4px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.ad-no {
  font-family: var(--el-font-family-mono, monospace);
}

.ad-dot {
  color: var(--el-text-color-disabled);
}

.ad-tags {
  display: flex;
  flex-shrink: 0;
  gap: 8px;
}

.ad-deleted-banner {
  padding: 10px 14px;
  margin-bottom: 16px;
  font-size: 13px;
  color: var(--el-color-danger);
  background-color: var(--el-color-danger-light-9);
  border: 1px solid var(--el-color-danger-light-7);
  border-radius: 6px;
}

.ad-desc {
  margin-bottom: 20px;
}

.ad-section {
  margin-bottom: 20px;
}

.ad-section:last-child {
  margin-bottom: 0;
}

.ad-section-title {
  padding-left: 9px;
  margin-bottom: 12px;
  font-size: 14px;
  font-weight: 600;
  line-height: 1;
  color: var(--el-text-color-primary);
  border-left: 3px solid var(--el-color-primary);
}

.ad-photos {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.ad-photo {
  width: 88px;
  height: 88px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}
</style>
