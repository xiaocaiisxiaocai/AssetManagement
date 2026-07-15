<script lang="ts" setup>
import type { MaterialDetail, MaterialStatus } from '#/api/material';

import { onBeforeUnmount, ref, watch } from 'vue';

import {
  ElAlert,
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElEmpty,
  ElImage,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

import { loadAssetImageObjectUrl } from '#/api/asset';
import { formatDate, formatDateTime } from '#/utils/date-format';

const props = defineProps<{
  detail: MaterialDetail | null;
  loading: boolean;
}>();
const visible = defineModel<boolean>('visible', { default: false });
const imageUrls = ref<string[]>([]);
const imagesLoading = ref(false);
const imageLoadError = ref('');
let imageLoadGeneration = 0;

function revokeImageObjectUrls() {
  imageUrls.value.forEach((url) => URL.revokeObjectURL(url));
  imageUrls.value = [];
}

function disposeImages() {
  imageLoadGeneration++;
  imagesLoading.value = false;
  imageLoadError.value = '';
  revokeImageObjectUrls();
}

onBeforeUnmount(disposeImages);

watch(
  [visible, () => props.detail?.material.images],
  async ([opened, images]) => {
    const generation = ++imageLoadGeneration;
    imagesLoading.value = false;
    imageLoadError.value = '';
    revokeImageObjectUrls();
    if (!opened || !images?.length) return;

    imagesLoading.value = true;
    const results = await Promise.allSettled(
      images.map(loadAssetImageObjectUrl),
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

const statusText: Record<MaterialStatus, string> = {
  0: '在用',
  1: '已退回厂商',
};

const actionText: Record<string, string> = {
  approve: '审批通过',
  direct_transfer: '直接转移',
  reject: '驳回',
  start: '发起流转',
};
</script>

<template>
  <ElDialog
    v-model="visible"
    align-center
    class="material-detail-dialog"
    title="测试料件详情"
    width="860px"
  >
    <div v-loading="loading" class="material-detail-body">
      <template v-if="detail">
        <ElDescriptions :column="2" border class="material-detail-descriptions">
          <ElDescriptionsItem label="料件编号">
            {{ detail.material.materialNo }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="名称">
            {{ detail.material.name }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="所属项目">
            {{ detail.material.projectName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="厂商/来源">
            {{ detail.material.vendorName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="型号">
            {{ detail.material.model ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="品牌">
            {{ detail.material.brand ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="数量">
            {{ detail.material.quantity }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="状态">
            <ElTag
              :type="detail.material.status === 0 ? 'success' : 'info'"
              size="small"
            >
              {{ statusText[detail.material.status] }}
            </ElTag>
            <ElTag
              v-if="detail.material.isDeleted"
              class="ml-1"
              size="small"
              type="danger"
            >
              已删除
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="归属部门">
            {{ detail.material.departmentName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="存放位置">
            {{ detail.material.locationName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="保管人">
            {{ detail.material.custodianName ?? '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="接收日期">
            {{ formatDate(detail.material.receivedDate) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="备注">
            {{ detail.material.remark ?? '-' }}
          </ElDescriptionsItem>
        </ElDescriptions>

        <section
          v-if="detail.material.images.length"
          v-loading="imagesLoading"
          class="material-photo-section"
        >
          <div class="material-photo-title">
            料件照片（{{ detail.material.images.length }} 张）
          </div>
          <ElAlert
            v-if="imageLoadError"
            :closable="false"
            :title="imageLoadError"
            class="material-photo-alert"
            show-icon
            type="warning"
          />
          <div v-if="imageUrls.length" class="material-photo-list">
            <ElImage
              v-for="(url, index) in imageUrls"
              :key="url"
              :alt="`${detail.material.name}料件照片 ${index + 1}`"
              :initial-index="index"
              :preview-src-list="imageUrls"
              :src="url"
              class="material-photo"
              fit="cover"
              preview-teleported
            >
              <template #error>
                <div class="material-photo-error">加载失败</div>
              </template>
            </ElImage>
          </div>
          <ElEmpty
            v-else-if="!imagesLoading"
            :image-size="48"
            description="照片加载失败"
          />
        </section>

        <section class="material-flow-section">
          <div class="material-flow-title">流转记录</div>
          <ElTimeline
            v-if="detail.records.length"
            class="material-flow-timeline"
          >
            <ElTimelineItem
              v-for="record in detail.records"
              :key="record.id"
              :timestamp="formatDateTime(record.operatedAt, { seconds: true })"
            >
              <div class="material-flow-record">
                <span class="font-medium">
                  {{ actionText[record.action] ?? record.action }}
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
      <ElEmpty v-else-if="!loading" description="暂无数据" />
    </div>
  </ElDialog>
</template>

<style scoped>
.material-detail-body {
  min-height: 220px;
}

.material-detail-descriptions {
  width: 100%;
}

.material-detail-descriptions :deep(.el-descriptions__label) {
  width: 96px;
  min-width: 96px;
  color: var(--el-text-color-regular);
  font-weight: 500;
  line-height: 22px;
  white-space: nowrap;
}

.material-detail-descriptions :deep(.el-descriptions__content) {
  min-width: 0;
  color: var(--el-text-color-primary);
  line-height: 22px;
  overflow-wrap: anywhere;
}

.material-photo-section {
  min-height: 112px;
  margin-top: 18px;
  padding-top: 14px;
  border-top: 1px solid var(--el-border-color-lighter);
}

.material-photo-title {
  margin-bottom: 12px;
  color: var(--el-text-color-primary);
  font-size: 14px;
  font-weight: 650;
}

.material-photo-alert {
  margin-bottom: 10px;
}

.material-photo-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.material-photo {
  width: 88px;
  height: 88px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.material-photo-error {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  color: var(--el-text-color-placeholder);
  font-size: 12px;
  background: var(--el-fill-color-light);
}

.material-flow-section {
  margin-top: 18px;
  padding-top: 14px;
  border-top: 1px solid var(--el-border-color-lighter);
}

.material-flow-title {
  margin-bottom: 12px;
  color: var(--el-text-color-primary);
  font-size: 14px;
  font-weight: 650;
}

.material-flow-timeline {
  padding-left: 2px;
}

.material-flow-record {
  color: var(--el-text-color-primary);
  line-height: 22px;
}

.material-flow-comment {
  margin-top: 2px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  line-height: 20px;
  overflow-wrap: anywhere;
}

@media (max-width: 768px) {
  .material-detail-descriptions :deep(.el-descriptions__label) {
    width: 82px;
    min-width: 82px;
  }
}
</style>
