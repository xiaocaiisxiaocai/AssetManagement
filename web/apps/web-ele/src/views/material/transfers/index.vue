<script lang="ts" setup>
import {
  approveFlowApi,
  listMyFlowsApi,
  listPendingFlowsApi,
  rejectFlowApi,
  type MaterialFlowItem,
} from '#/api/material';
import { getSettingsApi } from '#/api/base-data';
import {
  ElAlert,
  ElButton,
  ElDialog,
  ElInput,
  ElMessage,
  ElTabPane,
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTag,
  type TabPaneName,
} from 'element-plus';
import { onMounted, ref } from 'vue';

defineOptions({ name: 'MaterialTransfers' });

const activeTab = ref<'pending' | 'mine'>('pending');
const loading = ref(false);
const actionLoading = ref(false);
const pendingFlows = ref<MaterialFlowItem[]>([]);
const myFlows = ref<MaterialFlowItem[]>([]);
const approvalEnabled = ref(false);
const selected = ref<MaterialFlowItem | null>(null);
const detailVisible = ref(false);
const rejectVisible = ref(false);
const opinion = ref('同意');
const rejectReason = ref('');

async function loadSettings() {
  try {
    const settings = await getSettingsApi();
    const s = settings.find((x) => x.key === 'material.transfer.approval.enabled');
    approvalEnabled.value = s?.value?.trim().toLowerCase() === 'true';
  } catch {
    // 静默忽略，开关状态不影响页面主功能
  }
}

async function loadPending() {
  loading.value = true;
  try {
    pendingFlows.value = await listPendingFlowsApi();
  } catch (e: any) {
    ElMessage.error(e.message || '加载失败');
  } finally {
    loading.value = false;
  }
}

async function loadMine() {
  loading.value = true;
  try {
    myFlows.value = await listMyFlowsApi();
  } catch (e: any) {
    ElMessage.error(e.message || '加载失败');
  } finally {
    loading.value = false;
  }
}

async function onTabChange(tab: TabPaneName) {
  if (tab === 'pending') await loadPending();
  else await loadMine();
}

function openApprove(flow: MaterialFlowItem) {
  selected.value = flow;
  opinion.value = '同意';
  detailVisible.value = true;
}

function openReject(flow: MaterialFlowItem) {
  selected.value = flow;
  rejectReason.value = '';
  rejectVisible.value = true;
}

async function submitApprove() {
  if (!selected.value) return;
  actionLoading.value = true;
  try {
    const nodeId = selected.value.currentNodeIds.length > 0 ? selected.value.currentNodeIds[0] : undefined;
    await approveFlowApi(selected.value.id, opinion.value, nodeId);
    ElMessage.success('审批通过');
    detailVisible.value = false;
    await loadPending();
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败');
  } finally {
    actionLoading.value = false;
  }
}

async function submitReject() {
  if (!selected.value || !rejectReason.value.trim()) {
    ElMessage.warning('请填写驳回原因');
    return;
  }
  actionLoading.value = true;
  try {
    const nodeId = selected.value.currentNodeIds.length > 0 ? selected.value.currentNodeIds[0] : undefined;
    await rejectFlowApi(selected.value.id, rejectReason.value.trim(), nodeId);
    ElMessage.success('已驳回');
    rejectVisible.value = false;
    await loadPending();
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败');
  } finally {
    actionLoading.value = false;
  }
}

function statusTag(status: string) {
  if (status === 'pending') return { type: 'warning', label: '待审批' };
  if (status === 'approved') return { type: 'success', label: '已通过' };
  if (status === 'rejected') return { type: 'danger', label: '已驳回' };
  return { type: 'info', label: status };
}

onMounted(async () => {
  await Promise.all([loadSettings(), loadPending()]);
});
</script>

<template>
  <div class="p-4">
    <ElAlert
      v-if="!approvalEnabled"
      title="流转审批开关已关闭，料件转移将直接生效，无需审批"
      type="info"
      show-icon
      :closable="false"
      class="mb-4"
    />

    <ElTabs v-model="activeTab" @tab-change="onTabChange">
      <ElTabPane name="pending" label="待我审批">
        <ElTable :data="pendingFlows" v-loading="loading" border stripe class="mt-2">
          <ElTableColumn prop="flowNo" label="流转单号" width="180" />
          <ElTableColumn prop="materialNo" label="料件编号" width="160" />
          <ElTableColumn prop="materialName" label="料件名称" min-width="140" />
          <ElTableColumn prop="applicant" label="申请人" width="100" />
          <ElTableColumn prop="transferee" label="受让人" width="100" />
          <ElTableColumn prop="reason" label="原因" min-width="120" show-overflow-tooltip />
          <ElTableColumn prop="applyTime" label="申请时间" width="160">
            <template #default="{ row }">
              {{ new Date(row.applyTime).toLocaleString('zh-CN') }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="90">
            <template #default="{ row }">
              <ElTag :type="statusTag(row.status).type as any">
                {{ statusTag(row.status).label }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="操作" width="160" fixed="right">
            <template #default="{ row }">
              <template v-if="row.status === 'pending'">
                <ElButton size="small" type="primary" @click="openApprove(row)">通过</ElButton>
                <ElButton size="small" type="danger" @click="openReject(row)">驳回</ElButton>
              </template>
            </template>
          </ElTableColumn>
        </ElTable>
      </ElTabPane>

      <ElTabPane name="mine" label="我的发起">
        <ElTable :data="myFlows" v-loading="loading" border stripe class="mt-2">
          <ElTableColumn prop="flowNo" label="流转单号" width="180" />
          <ElTableColumn prop="materialNo" label="料件编号" width="160" />
          <ElTableColumn prop="materialName" label="料件名称" min-width="140" />
          <ElTableColumn prop="transferee" label="受让人" width="100" />
          <ElTableColumn prop="reason" label="原因" min-width="120" show-overflow-tooltip />
          <ElTableColumn prop="applyTime" label="申请时间" width="160">
            <template #default="{ row }">
              {{ new Date(row.applyTime).toLocaleString('zh-CN') }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="90">
            <template #default="{ row }">
              <ElTag :type="statusTag(row.status).type as any">
                {{ statusTag(row.status).label }}
              </ElTag>
            </template>
          </ElTableColumn>
        </ElTable>
      </ElTabPane>
    </ElTabs>

    <!-- 审批通过对话框 -->
    <ElDialog v-model="detailVisible" title="审批意见" width="420px">
      <div class="mb-2 text-sm text-gray-500">
        料件：{{ selected?.materialName }}（{{ selected?.materialNo }}）→ {{ selected?.transferee }}
      </div>
      <ElInput
        v-model="opinion"
        type="textarea"
        :rows="3"
        placeholder="审批意见（可选）"
      />
      <template #footer>
        <ElButton @click="detailVisible = false">取消</ElButton>
        <ElButton type="primary" :loading="actionLoading" @click="submitApprove">确认通过</ElButton>
      </template>
    </ElDialog>

    <!-- 驳回对话框 -->
    <ElDialog v-model="rejectVisible" title="驳回原因" width="420px">
      <div class="mb-2 text-sm text-gray-500">
        料件：{{ selected?.materialName }}（{{ selected?.materialNo }}）
      </div>
      <ElInput
        v-model="rejectReason"
        type="textarea"
        :rows="3"
        placeholder="请填写驳回原因（必填）"
      />
      <template #footer>
        <ElButton @click="rejectVisible = false">取消</ElButton>
        <ElButton type="danger" :loading="actionLoading" @click="submitReject">确认驳回</ElButton>
      </template>
    </ElDialog>
  </div>
</template>
