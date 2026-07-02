<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';

import { onMounted, reactive, ref } from 'vue';

import { getPendingReturnsApi, confirmReturnApi } from '#/api/workflow';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';

import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'ConfirmReturn' });

const loading = ref(false);
const confirmingIds = ref(new Set<number>());
const flows = ref<ApprovalFlow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));

const query = reactive({
  page: 1,
  pageSize: 20,
});

const allFlowsCache = ref<ApprovalFlow[]>([]);

async function loadData() {
  loading.value = true;
  try {
    const allFlows = await getPendingReturnsApi();
    allFlowsCache.value = allFlows.filter(
      (f) => f.bizType === 'borrow' && f.status === 'approved' && !f.confirmedAt
    );
    total.value = allFlowsCache.value.length;
    query.page = 1;
    updatePage();
  } finally {
    loading.value = false;
  }
}

function updatePage() {
  const start = (query.page - 1) * query.pageSize;
  flows.value = allFlowsCache.value.slice(start, start + query.pageSize);
}

function onPageSizeChange() {
  query.page = 1;
  updatePage();
}

async function confirmReturn(row: ApprovalFlow) {
  try {
    await ElMessageBox.confirm(
      `确认资产「${row.assetName}」已由「${row.applicant}」归还入库？`,
      '确认入库',
      { type: 'warning', confirmButtonText: '确认', cancelButtonText: '取消' }
    );
  } catch {
    return;
  }

  confirmingIds.value.add(row.id);
  try {
    await confirmReturnApi(row.id);
    ElMessage.success('确认入库成功，资产已恢复在库状态');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    confirmingIds.value.delete(row.id);
  }
}

function formatTime(time: string | Date | null | undefined) {
  if (!time) return '-';
  const date = new Date(time);
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function bizText(type: string) {
  return { borrow: '借用', return: '归还', transfer: '转让' }[type] ?? type;
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">待确认入库</h2>
        </div>
        <div class="page-actions">
          <span class="page-header-total">共 <span>{{ total }}</span> 件待确认</span>
        </div>
      </div>

      <div class="table-panel-with-toolbar">
        <ElTable v-loading="loading" :data="flows" border height="100%">
          <ElTableColumn label="流程编号" min-width="160" prop="flowNo" />
          <ElTableColumn class-name="hide-on-mobile" label="资产编号" min-width="140" prop="assetNo" />
          <ElTableColumn label="资产名称" min-width="200" prop="assetName" />
          <ElTableColumn label="借用类型" width="100" align="center">
            <template #default="{ row }">
              <ElTag type="success" size="small">{{ bizText(row.bizType) }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="借用人" min-width="130" prop="applicant" />
          <ElTableColumn class-name="hide-on-mobile" label="借用部门" min-width="150" prop="applicantDept" />
          <ElTableColumn label="应归还日期" min-width="140" prop="returnDate" />
          <ElTableColumn class-name="hide-on-mobile" label="申请时间" min-width="180">
            <template #default="{ row }">
              {{ formatTime(row.applyTime) }}
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="借用事由" min-width="200" prop="reason" show-overflow-tooltip />
          <ElTableColumn fixed="right" label="操作" width="120" align="center">
            <template #default="{ row }">
              <ElButton
                :loading="confirmingIds.has(row.id)"
                link
                type="primary"
                size="small"
                @click="confirmReturn(row)"
              >
                确认入库
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>

        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ total }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect v-model="query.pageSize" style="width: 92px" @change="onPageSizeChange">
              <ElOption
                v-for="size in pageSizeOptions"
                :key="size"
                :label="`${size}`"
                :value="size"
              />
            </ElSelect>
          </div>
          <ElPagination
            v-model:current-page="query.page"
            :page-size="query.pageSize"
            :total="total"
            background
            layout="prev, pager, next"
            @current-change="updatePage"
          />
        </div>
      </div>
    </div>
  </re-page>
</template>

<style scoped>
.page-header-total {
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.page-header-total span {
  color: var(--el-color-primary);
  font-size: 20px;
  font-weight: 600;
}
</style>
