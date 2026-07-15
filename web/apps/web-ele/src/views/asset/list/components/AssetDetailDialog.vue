<script lang="ts" setup>
import type { AssetDetail, AssetStatus } from '#/api/asset';

import { onBeforeUnmount, ref, watch } from 'vue';

import {
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElEmpty,
  ElImage,
  ElTable,
  ElTableColumn,
  ElTabPane,
  ElTabs,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

import { loadAssetImageObjectUrl } from '#/api/asset';
import { formatDate, formatDateTime } from '#/utils/date-format';

const props = defineProps<{
  detail: AssetDetail | null;
  loading: boolean;
}>();

const visible = defineModel<boolean>('visible', { default: false });
const imageUrls = ref<string[]>([]);
const activeTab = ref('basic');
let imageLoadGeneration = 0;

function revokeImageObjectUrls() {
  imageUrls.value.forEach((url) => URL.revokeObjectURL(url));
  imageUrls.value = [];
}

onBeforeUnmount(revokeImageObjectUrls);

watch(
  [visible, () => props.detail?.asset.images],
  async ([opened, images]) => {
    const generation = ++imageLoadGeneration;
    revokeImageObjectUrls();
    if (!opened || !images?.length) return;
    const urls = await Promise.all(images.map(loadAssetImageObjectUrl));
    if (generation !== imageLoadGeneration || !visible.value) {
      urls.forEach((url) => URL.revokeObjectURL(url));
      return;
    }
    imageUrls.value = urls;
  },
  { deep: true },
);

watch(visible, (opened) => {
  if (opened) activeTab.value = 'basic';
});

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
  withdrawn: { tag: 'info', text: '已撤回' },
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

const actionTypeText: Record<string, string> = {
  BUSINESS: '业务',
  CLEANUP: '清理',
  DELETE: '删除',
  GET: '查看',
  PATCH: '修改',
  POST: '新增',
  PUT: '修改',
  REMIND: '催办',
};

const actionTypeTag: Record<
  string,
  'danger' | 'info' | 'primary' | 'success' | 'warning'
> = {
  BUSINESS: 'primary',
  CLEANUP: 'info',
  DELETE: 'danger',
  GET: 'info',
  PATCH: 'warning',
  POST: 'success',
  PUT: 'warning',
  REMIND: 'warning',
};

function actionText(action: null | string | undefined) {
  if (!action) return '—';
  return actionTypeText[action.toUpperCase()] ?? action;
}

function actionTag(action: null | string | undefined) {
  return actionTypeTag[(action ?? '').toUpperCase()] ?? 'info';
}

function summaryText(summary: null | string | undefined) {
  if (!summary) return '—';
  return summary
    .replace(/^(DELETE|GET|PATCH|POST|PUT)\s+/i, '')
    .replace(/\bborrow\b/gi, '借用')
    .replace(/\btransfer\b/gi, '转让')
    .replace(/\breturn\b/gi, '归还');
}
</script>

<template>
  <ElDialog
    v-model="visible"
    class="asset-detail-dialog"
    title="资产详情"
    width="min(900px, 92vw)"
  >
    <div v-loading="loading" class="asset-detail">
      <template v-if="detail">
        <header class="ad-header">
          <div class="ad-header-main">
            <h3 class="ad-title">{{ detail.asset.name }}</h3>
            <div class="ad-sub">
              <span class="ad-sub-tag">编号</span>
              <span class="ad-no">{{ detail.asset.assetNo }}</span>
              <span class="ad-dot">·</span>
              <span class="ad-sub-tag">分类</span>
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
          该资产已于
          {{ formatDateTime(detail.asset.deletedAt, { empty: '—' }) }}
          删除，可由有权限的人员在列表中「撤销删除」恢复或「彻底删除」。
        </div>

        <ElTabs v-model="activeTab" class="ad-tabs">
          <ElTabPane label="基本信息" name="basic">
            <div class="ad-tab-content">
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
          <ElDescriptionsItem label="购入日期">
            {{ formatDate(detail.asset.purchaseDate, '—') }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="资产登记日期">
            {{ formatDate(detail.asset.registrationTime, '—') }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="目前状况">
            {{ detail.asset.currentCondition || '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="备注">
            {{ detail.asset.remark || '—' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem
            :span="detail.asset.isDeleted ? 1 : 2"
            label="创建时间"
          >
            {{ formatDateTime(detail.asset.createdAt, { empty: '—' }) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="detail.asset.isDeleted" label="删除时间">
            {{ formatDateTime(detail.asset.deletedAt, { empty: '—' }) }}
          </ElDescriptionsItem>
        </ElDescriptions>

        <section
          v-if="detail.asset.images && detail.asset.images.length"
          class="ad-section"
        >
          <div class="ad-section-title">资产照片</div>
          <div class="ad-photos">
            <ElImage
              v-for="(url, i) in imageUrls"
              :key="i"
              :alt="`${detail.asset.name}资产照片 ${i + 1}`"
              :initial-index="i"
              :preview-src-list="imageUrls"
              :src="url"
              class="ad-photo"
              fit="cover"
              preview-teleported
            />
          </div>
        </section>

            <ElEmpty
              v-if="!detail.asset.images?.length"
              :image-size="56"
              description="暂无资产照片"
            />
            </div>
          </ElTabPane>

          <ElTabPane :label="`流转记录 ${detail.flows.length}`" name="flows">
            <div class="ad-tab-content">
        <section class="ad-section ad-timeline-section">
          <ElTimeline v-if="detail.flows.length">
            <ElTimelineItem
              v-for="flow in detail.flows"
              :key="flow.id"
              :timestamp="
                formatDateTime(
                  flow.status === 'withdrawn'
                    ? flow.withdrawnAt
                    : formatDateTime(flow.applyTime),
                )
              "
              :type="flowStatusMeta[flow.status]?.tag ?? 'primary'"
            >
              <div class="text-sm">
                <span class="font-medium">{{ flowTitle(flow) }}</span>
                <span class="text-gray-500"> · {{ flow.applicant }}</span>
                <span v-if="flow.transferee" class="text-gray-500">
                  → {{ flow.transferee }}</span
                >
              </div>
              <div v-if="flow.reason" class="text-xs text-gray-400">
                事由：{{ flow.reason }}
              </div>
              <div v-if="flow.returnDate" class="text-xs text-gray-400">
                应归还：{{ formatDate(flow.returnDate) }}
              </div>
            </ElTimelineItem>
          </ElTimeline>
          <ElEmpty v-else :image-size="56" description="暂无流转记录" />
        </section>
            </div>
          </ElTabPane>

          <ElTabPane :label="`操作日志 ${detail.recentLogs.length}`" name="logs">
            <div class="ad-tab-content">
        <section class="ad-section">
          <ElTable
            v-if="detail.recentLogs.length"
            :data="detail.recentLogs"
            border
            max-height="480"
            size="small"
            stripe
          >
            <ElTableColumn label="时间" width="170">
              <template #default="{ row }">
                {{ formatDateTime(row.occurredAt, { empty: '—', seconds: true }) }}
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
                <span class="ad-log-summary">{{
                  summaryText(row.summary)
                }}</span>
              </template>
            </ElTableColumn>
          </ElTable>
          <ElEmpty v-else :image-size="56" description="暂无操作日志" />
        </section>
            </div>
          </ElTabPane>
        </ElTabs>
      </template>
      <ElEmpty v-else-if="!loading" description="暂无数据" />
    </div>
  </ElDialog>
</template>

<style scoped>
.asset-detail {
  min-height: 420px;
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

.ad-sub-tag {
  padding: 1px 6px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
  background-color: var(--el-fill-color-light);
  border-radius: 4px;
}

.ad-log-summary {
  font-size: 12px;
  color: var(--el-text-color-secondary);
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
  margin-bottom: 16px;
}

.ad-tabs :deep(.el-tabs__header) {
  margin-bottom: 0;
}

.ad-tabs :deep(.el-tabs__item) {
  min-width: 112px;
  height: 44px;
  font-weight: 500;
}

.ad-tab-content {
  min-height: 330px;
  max-height: 56vh;
  padding: 18px 2px 4px;
  overflow: auto;
}

.ad-timeline-section {
  padding: 6px 12px 0 4px;
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

@media (max-width: 640px) {
  .ad-header {
    flex-direction: column;
  }

  .ad-sub {
    flex-wrap: wrap;
  }

  .ad-tabs :deep(.el-tabs__item) {
    min-width: auto;
    padding: 0 12px;
  }
}
</style>
