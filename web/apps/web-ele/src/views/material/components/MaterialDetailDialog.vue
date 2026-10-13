<script lang="ts" setup>
import type { MaterialDetail, MaterialStatus } from '#/api/material';

import { onBeforeUnmount, ref, watch } from 'vue';

import { ElDialog, ElEmpty } from 'element-plus';

import { loadAssetImageObjectUrl } from '#/api/asset';

import MaterialDetailPhotos from './MaterialDetailPhotos.vue';
import MaterialDetailRecords from './MaterialDetailRecords.vue';
import MaterialDetailSummary from './MaterialDetailSummary.vue';

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

const statusText: Record<MaterialStatus, string> = {
  0: '在用',
  1: '已退回厂商',
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
    <div class="material-detail-body" v-loading="loading">
      <template v-if="detail">
        <MaterialDetailSummary :detail="detail" :status-text="statusText" />
        <MaterialDetailPhotos
          :detail="detail"
          :image-load-error="imageLoadError"
          :image-urls="imageUrls"
          :images-loading="imagesLoading"
        />
        <MaterialDetailRecords :detail="detail" />
      </template>
      <ElEmpty v-else-if="!loading" description="暂无数据" />
    </div>
  </ElDialog>
</template>

<style scoped src="./MaterialDetailDialog.css"></style>
