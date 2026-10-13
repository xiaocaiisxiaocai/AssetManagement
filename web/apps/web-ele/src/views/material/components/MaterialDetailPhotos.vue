<script lang="ts" setup>
import type { MaterialDetail } from '#/api/material';

import { ElAlert, ElEmpty, ElImage } from 'element-plus';

defineProps<{
  detail: MaterialDetail;
  imageLoadError: string;
  imagesLoading: boolean;
  imageUrls: string[];
}>();
</script>

<template>
  <section
    v-if="detail.material.images.length > 0"
    class="material-photo-section"
    v-loading="imagesLoading"
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
    <div v-if="imageUrls.length > 0" class="material-photo-list">
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
</template>

<style scoped src="./MaterialDetailDialog.css"></style>
