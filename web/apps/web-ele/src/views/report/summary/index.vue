<script lang="ts" setup>
import type { AssetSummary } from '#/api/report';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import { ElButton } from 'element-plus';

import { exportAssetSummaryApi, getAssetSummaryApi } from '#/api/report';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';
import { buildReportActionAccess } from '#/views/permissions/action-access';

import SummaryStatCards from './SummaryStatCards.vue';
import SummaryTables from './SummaryTables.vue';

defineOptions({ name: 'ReportSummary' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const reportActionAccess = computed(() =>
  buildReportActionAccess(hasAccessByCodes),
);
const loading = ref(false);
const exporting = ref(false);
const summary = ref<AssetSummary>({
  available: 0,
  borrowed: 0,
  byCategory: [],
  byDept: [],
  total: 0,
});

async function loadData() {
  loading.value = true;
  try {
    summary.value = await getAssetSummaryApi();
  } finally {
    loading.value = false;
  }
}

function goCategoryAssets(categoryCode: string) {
  if (!categoryCode) return;
  runHandled(
    router.push({
      path: '/asset/list',
      query: { categoryCode },
    }),
  );
}

async function exportSummary() {
  if (exporting.value) return;
  exporting.value = true;
  try {
    const response = await exportAssetSummaryApi();
    downloadBlob(response.data, '资产汇总.xlsx');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    exporting.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">资产汇总</h2>
        </div>
        <div class="page-actions">
          <ElButton
            v-if="reportActionAccess.canExport"
            :loading="exporting"
            @click="exportSummary"
          >
            导出
          </ElButton>
        </div>
      </div>

      <SummaryStatCards :summary="summary" />
      <SummaryTables
        :loading="loading"
        :summary="summary"
        @category-click="goCategoryAssets"
      />
    </div>
  </re-page>
</template>
