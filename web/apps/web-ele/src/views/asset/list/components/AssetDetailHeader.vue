<script lang="ts" setup>
import type { AssetDetail, AssetStatus } from '#/api/asset';

import { ElTag } from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

defineProps<{
  detail: AssetDetail;
  statusMeta: (status: AssetStatus) => {
    label: string;
    tag: 'danger' | 'info' | 'success' | 'warning';
  };
}>();
</script>

<template>
  <div>
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
  </div>
</template>

<style scoped src="./AssetDetailDialog.css"></style>
