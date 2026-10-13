<script lang="ts" setup>
import type { AssetDetail } from '#/api/asset';

import {
  ElAlert,
  ElDescriptions,
  ElDescriptionsItem,
  ElEmpty,
  ElImage,
} from 'element-plus';

import { formatDate, formatDateTime } from '#/utils/date-format';

defineProps<{
  detail: AssetDetail;
  imageLoadError: string;
  imagesLoading: boolean;
  imageUrls: string[];
}>();
</script>

<template>
  <div class="ad-tab-content">
    <ElDescriptions :column="2" border class="ad-desc" size="small">
      <ElDescriptionsItem label="归属部门">
        {{ detail.asset.departmentName ?? '—' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="存放位置">
        {{ detail.asset.locationName ?? '—' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="当前保管人">
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
      v-if="detail.asset.images && detail.asset.images.length > 0"
      class="ad-section"
      v-loading="imagesLoading"
    >
      <div class="ad-section-title">资产照片</div>
      <ElAlert
        v-if="imageLoadError"
        :closable="false"
        :title="imageLoadError"
        class="mb-3"
        show-icon
        type="warning"
      />
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
</template>

<style scoped src="./AssetDetailDialog.css"></style>
