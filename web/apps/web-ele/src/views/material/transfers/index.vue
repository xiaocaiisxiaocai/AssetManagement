<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import { onMounted, ref } from 'vue';

import {
  ElAlert,
  ElButton,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
  ElTabPane,
  ElTabs,
  ElTag,
} from 'element-plus';

import {
  approveFlowApi,
  listMyFlowsApi,
  listPendingFlowsApi,
  rejectFlowApi,
} from '#/api/material';

defineOptions({ name: 'MaterialTransfers' });

const activeTab = ref('pending');
const pendingLoading = ref(false);
const mineLoading = ref(false);
const pendingFlows = ref<MaterialFlowItem[]>([]);
const myFlows = ref<MaterialFlowItem[]>([]);

const statusMeta: Record<string, { label: string; tag: 'info' | 'success' | 'warning' }> = {
  approved: { label: '已通过', tag: 'success' },
  pending: { label: '审批中', tag: 'warning' },
  rejected: { label: '已驳回', tag: 'info' },
};

async function loadPending() {
  pendingLoading.value = true;
  try {
    pendingFlows.value = await listPendingFlowsApi();
  } finally {
    pendingLoading.value = false;
  }
}

async function loadMine() {
  mineLoading.value = true;
  try {
    myFlows.value = await listMyFlowsApi();
  } finally {
    mineLoading.value = false;
  }
}

async function approve(row: MaterialFlowItem) {
  await ElMessageBox.confirm(
    `确认通过料件「${row.materialName}」的流转申请？`,
    '审批通过',
    { type: 'warning' },
  );
  await approveFlowApi(row.id, '同意');
  ElMessage.success('已通过');
  await loadPending();
}

async function reject(row: MaterialFlowItem) {
  const { value } = await ElMessageBox.prompt('请输入驳回原因', '驳回', {
    inputPlaceholder: '驳回原因',
  });
  await rejectFlowApi(row.id, value || '不同意');
  ElMessage.success('已驳回');
  await loadPending();
}

function metaOf(status: string) {
  return statusMeta[status] ?? { label: status, tag: 'info' as const };
}

function onTabChange(name: number | string) {
  if (name === 'mine') {
    void loadMine();
  } else {
    void loadPending();
  }
}

onMounted(loadPending);
</script>

<template>
  <re-page>
    <div class="p-5">
      <ElAlert
        :closable="false"
        class="mb-4"
        title="测试料件转移审批是否启用,由系统参数 material.transfer.approval.enabled 控制；关闭时转移直接生效,本页无待办。"
        type="info"
      />
      <ElTabs v-model="activeTab" @tab-change="onTabChange">
        <ElTabPane label="待我审批" name="pending">
          <ElTable v-loading="pendingLoading" :data="pendingFlows" border stripe>
            <ElTableColumn label="流转单号" min-width="180" prop="flowNo" />
            <ElTableColumn label="料件编号" min-width="160" prop="materialNo" />
            <ElTableColumn label="料件名称" min-width="140" prop="materialName" show-overflow-tooltip />
            <ElTableColumn label="发起人" min-width="100" prop="applicant" />
            <ElTableColumn label="受让人" min-width="100" prop="transferee" />
            <ElTableColumn label="原因" min-width="160" prop="reason" show-overflow-tooltip />
            <ElTableColumn align="center" fixed="right" label="操作" width="160">
              <template #default="{ row }">
                <ElButton link size="small" type="success" @click="approve(row)">
                  通过
                </ElButton>
                <ElButton link size="small" type="danger" @click="reject(row)">
                  驳回
                </ElButton>
              </template>
            </ElTableColumn>
          </ElTable>
        </ElTabPane>
        <ElTabPane label="我的发起" name="mine">
          <ElTable v-loading="mineLoading" :data="myFlows" border stripe>
            <ElTableColumn label="流转单号" min-width="180" prop="flowNo" />
            <ElTableColumn label="料件编号" min-width="160" prop="materialNo" />
            <ElTableColumn label="料件名称" min-width="140" prop="materialName" show-overflow-tooltip />
            <ElTableColumn label="受让人" min-width="100" prop="transferee" />
            <ElTableColumn label="原因" min-width="160" prop="reason" show-overflow-tooltip />
            <ElTableColumn align="center" label="状态" width="110">
              <template #default="{ row }">
                <ElTag :type="metaOf(row.status).tag" size="small">
                  {{ metaOf(row.status).label }}
                </ElTag>
              </template>
            </ElTableColumn>
          </ElTable>
        </ElTabPane>
      </ElTabs>
    </div>
  </re-page>
</template>
