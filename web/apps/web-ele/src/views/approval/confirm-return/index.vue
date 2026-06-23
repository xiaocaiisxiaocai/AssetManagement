<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';

import { computed, onMounted, reactive, ref } from 'vue';
import { useDebounceFn } from '@vueuse/core';

import { getPendingReturnsApi, confirmReturnApi } from '#/api/workflow';

import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElPagination,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'ConfirmReturn' });

const loading = ref(false);
const confirming = ref(false);
const flows = ref<ApprovalFlow[]>([]);
const total = ref(0);

const query = reactive({
  page: 1,
  pageSize: 20,
});

const pendingFlows = computed(() => {
  const allFlows = flows.value.filter(
    (f) => f.bizType === 'borrow' && f.status === 'approved' && !f.confirmedAt
  );
  return allFlows;
});

async function loadData() {
  loading.value = true;
  try {
    const allFlows = await getPendingReturnsApi();
    const pending = allFlows.filter(
      (f) => f.bizType === 'borrow' && f.status === 'approved' && !f.confirmedAt
    );
    flows.value = pending;
    total.value = pending.length;
  } finally {
    loading.value = false;
  }
}

async function confirmReturn(row: ApprovalFlow) {
  await ElMessageBox.confirm(
    `确认资产「${row.assetName}」已由「${row.applicant}」归还入库？`,
    '确认入库',
    {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    }
  );

  confirming.value = true;
  try {
    await confirmReturnApi(row.id);
    ElMessage.success('确认入库成功，资产已恢复在库状态');
    await loadData();
  } catch (err: any) {
    const message = err?.response?.data?.message || '确认入库失败';
    ElMessage.error(message);
  } finally {
    confirming.value = false;
  }
}

// 防抖版本的确认入库方法,防止用户快速点击导致重复确认
const debouncedConfirmReturn = useDebounceFn(confirmReturn, 300);

function formatTime(time: string | Date) {
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

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">待确认入库</h2>
        </div>
        <div class="text-sm font-medium">
          共 <span class="text-lg text-primary">{{ total }}</span> 件待确认
        </div>
      </div>

      <div v-if="pendingFlows.length === 0" class="rounded border border-dashed bg-card p-10 text-center text-muted-foreground">
        暂无待确认的资产
      </div>

      <div v-else class="space-y-3">
        <div class="flex items-center justify-between text-sm text-muted-foreground">
          <span>共 {{ total }} 件资产</span>
        </div>

        <ElTable v-loading="loading" :data="pendingFlows" border>
          <ElTableColumn label="流程编号" min-width="160" prop="flowNo" />
          <ElTableColumn label="资产编号" min-width="140" prop="assetNo" />
          <ElTableColumn label="资产名称" min-width="200" prop="assetName" />
          <ElTableColumn label="借用类型" width="100">
            <template #default="{ row }">
              <ElTag>{{ bizText(row.bizType) }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="借用人" min-width="130" prop="applicant" />
          <ElTableColumn label="借用部门" min-width="150" prop="applicantDept" />
          <ElTableColumn label="应归还日期" min-width="140" prop="returnDate" />
          <ElTableColumn label="申请时间" min-width="180">
            <template #default="{ row }">
              {{ formatTime(row.applyTime) }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="借用事由" min-width="200" prop="reason" show-overflow-tooltip />
          <ElTableColumn fixed="right" label="操作" width="120">
            <template #default="{ row }">
              <ElButton
                :loading="confirming"
                link
                type="primary"
                @click="debouncedConfirmReturn(row)"
              >
                确认入库
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>

        <div v-if="total > query.pageSize" class="flex justify-end">
          <ElPagination
            v-model:current-page="query.page"
            :page-size="query.pageSize"
            :total="total"
            background
            layout="prev, pager, next, jumper"
          />
        </div>
      </div>
    </div>
  </re-page>
</template>

<style scoped>
:deep(.el-table) {
  font-size: 14px;
}
</style>
