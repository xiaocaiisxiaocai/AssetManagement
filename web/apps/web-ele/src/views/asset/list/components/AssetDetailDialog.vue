<script lang="ts" setup>
import type { AssetDetail, AssetStatus } from '#/api/asset';

import { onBeforeUnmount, ref, watch } from 'vue';

import { ElDialog, ElEmpty, ElTabPane, ElTabs } from 'element-plus';

import { loadAssetImageObjectUrl } from '#/api/asset';

import { custodyTimelineCount } from './asset-detail-timeline';
import AssetDetailBasicPane from './AssetDetailBasicPane.vue';
import AssetDetailFlowsPane from './AssetDetailFlowsPane.vue';
import AssetDetailHeader from './AssetDetailHeader.vue';
import AssetDetailLogsPane from './AssetDetailLogsPane.vue';

const props = defineProps<{
  detail: AssetDetail | null;
  loading: boolean;
}>();

const visible = defineModel<boolean>('visible', { default: false });
const imageUrls = ref<string[]>([]);
const imagesLoading = ref(false);
const imageLoadError = ref('');
const activeTab = ref('basic');
let imageLoadGeneration = 0;

function revokeImageObjectUrls() {
  imageUrls.value.forEach((url) => URL.revokeObjectURL(url));
  imageUrls.value = [];
}

function disposeImages() {
  imageLoadGeneration += 1;
  imagesLoading.value = false;
  imageLoadError.value = '';
  revokeImageObjectUrls();
}

onBeforeUnmount(disposeImages);

watch(
  [visible, () => props.detail?.asset.images],
  async ([opened, images]) => {
    const generation = ++imageLoadGeneration;
    imagesLoading.value = false;
    imageLoadError.value = '';
    revokeImageObjectUrls();
    if (!opened || !images?.length) return;
    imagesLoading.value = true;
    const results = await Promise.allSettled(
      images.map((image) => loadAssetImageObjectUrl(image)),
    );
    const urls = results.flatMap((result) =>
      result.status === 'fulfilled' ? [result.value] : [],
    );
    if (generation !== imageLoadGeneration || !visible.value) {
      urls.forEach((url) => URL.revokeObjectURL(url));
      return;
    }
    imageUrls.value = urls;
    const failedCount = results.length - urls.length;
    if (failedCount > 0) {
      imageLoadError.value = `有 ${failedCount} 张照片加载失败，请稍后重试`;
    }
    imagesLoading.value = false;
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
  extension: '延期',
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
    .replaceAll(/\bborrow\b/gi, '借用')
    .replaceAll(/\bextension\b/gi, '延期')
    .replaceAll(/\btransfer\b/gi, '转让')
    .replaceAll(/\breturn\b/gi, '归还');
}
</script>

<template>
  <ElDialog
    v-model="visible"
    class="asset-detail-dialog"
    title="资产详情"
    width="min(900px, 92vw)"
  >
    <div class="asset-detail" v-loading="loading">
      <template v-if="detail">
        <AssetDetailHeader :detail="detail" :status-meta="statusMeta" />

        <ElTabs v-model="activeTab" class="ad-tabs">
          <ElTabPane label="基本信息" name="basic">
            <AssetDetailBasicPane
              :detail="detail"
              :image-load-error="imageLoadError"
              :image-urls="imageUrls"
              :images-loading="imagesLoading"
            />
          </ElTabPane>
          <ElTabPane
            :label="`流转记录 ${custodyTimelineCount(detail.flows.length)}`"
            name="flows"
          >
            <AssetDetailFlowsPane
              :detail="detail"
              :flow-status-meta="flowStatusMeta"
              :flow-title="flowTitle"
            />
          </ElTabPane>
          <ElTabPane
            :label="`操作日志 ${detail.recentLogs.length}`"
            name="logs"
          >
            <AssetDetailLogsPane
              :action-tag="actionTag"
              :action-text="actionText"
              :detail="detail"
              :summary-text="summaryText"
            />
          </ElTabPane>
        </ElTabs>
      </template>
      <ElEmpty v-else-if="!loading" description="暂无数据" />
    </div>
  </ElDialog>
</template>

<style scoped src="./AssetDetailDialog.css"></style>
