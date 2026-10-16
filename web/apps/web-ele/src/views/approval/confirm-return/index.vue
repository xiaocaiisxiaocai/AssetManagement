<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';

import { onMounted, reactive, ref } from 'vue';

import { ElMessage, ElMessageBox } from 'element-plus';

import { confirmReturnApi, getPendingReturnsPageApi } from '#/api/workflow';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';

import ConfirmReturnFilter from './ConfirmReturnFilter.vue';
import ConfirmReturnTable from './ConfirmReturnTable.vue';

defineOptions({ name: 'ConfirmReturn' });

const loading = ref(false);
const confirmingIds = ref(new Set<number>());
const flows = ref<ApprovalFlow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));

const query = reactive({
  keyword: '',
  returnDate: '',
  page: 1,
  pageSize: 20,
});
const queryModel = reactiveObjectModel(query);

const listRequestGuard = createLatestRequestGuard();

async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  try {
    const result = await getPendingReturnsPageApi({
      keyword: query.keyword.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      returnDate: query.returnDate || undefined,
    });
    if (!listRequestGuard.isLatest(requestGeneration)) return;
    const lastPage = Math.max(1, Math.ceil(result.total / query.pageSize));
    if (query.page > lastPage) {
      query.page = lastPage;
      await loadData();
      return;
    }
    flows.value = result.items;
    total.value = result.total;
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

function onPageSizeChange() {
  query.page = 1;
  runHandled(loadData());
}

function search() {
  query.page = 1;
  runHandled(loadData());
}

function resetQuery() {
  query.keyword = '';
  query.returnDate = '';
  query.page = 1;
  runHandled(loadData());
}

async function confirmReturn(row: ApprovalFlow) {
  try {
    await ElMessageBox.confirm(
      `请确认已实际收回「${row.applicant}」归还的资产「${row.assetName}」（${row.assetNo}）。确认后资产将恢复为可用状态，并优先恢复至借出前保管人；借出前保管人不可用时，由本次确认的所属组织负责人接收入库。`,
      '确认资产已归还',
      {
        type: 'warning',
        confirmButtonText: '确认已收回',
        cancelButtonText: '暂不确认',
        closeOnClickModal: false,
        closeOnPressEscape: false,
      },
    );
  } catch {
    return;
  }

  confirmingIds.value.add(row.id);
  try {
    await confirmReturnApi(row.id);
    ElMessage.success('归还确认成功，资产已恢复可用状态');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    confirmingIds.value.delete(row.id);
  }
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container page-fit">
      <ConfirmReturnFilter
        v-model:query="queryModel"
        @reset="resetQuery"
        @search="search"
      />

      <ConfirmReturnTable
        v-model:query="queryModel"
        :confirming-ids="confirmingIds"
        :flows="flows"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :total="total"
        @confirm="confirmReturn"
        @page-change="loadData"
        @page-size-change="onPageSizeChange"
      />
    </div>
  </re-page>
</template>
