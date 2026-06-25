<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';
import type { UserDto } from '#/api/user';
import { onMounted, ref, computed } from 'vue';
import { useDebounceFn } from '@vueuse/core';
import {
  addSignFlowApi,
  approveFlowApi,
  BpmnTokenStatus,
  getPendingApprovalsApi,
  rejectFlowApi,
} from '#/api/workflow';
import { getUserListApi } from '#/api/user';
import {
  ElButton,
  ElDialog,
  ElDescriptions,
  ElDescriptionsItem,
  ElInput,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
  ElMessage,
} from 'element-plus';

defineOptions({ name: 'ApprovalPending' });

const loading = ref(false);
const actionLoading = ref(false);
const addSignLoading = ref(false);
const detailVisible = ref(false);
const addSignVisible = ref(false);
const selected = ref<ApprovalFlow | null>(null);
const flows = ref<ApprovalFlow[]>([]);
const users = ref<UserDto[]>([]);
const opinion = ref('同意');
const addSignUser = ref('');
const selectedNodeId = ref('');

async function loadData() {
  loading.value = true;
  try {
    const [pending, userPage] = await Promise.all([
      getPendingApprovalsApi(),
      getUserListApi('', 1, 200),
    ]);
    flows.value = pending;
    users.value = userPage.items.filter((user) => user.isActive);
  } catch (error: any) {
    ElMessage.error(error.message || '加载失败');
  } finally {
    loading.value = false;
  }
}

function openDetail(flow: ApprovalFlow) {
  selected.value = flow;
  opinion.value = '同意';
  addSignUser.value = '';
  selectedNodeId.value = flow.currentNodeIds[0] || '';
  detailVisible.value = true;
}

// 获取当前活跃节点信息
const currentNodeInfo = computed(() => {
  if (!selected.value) return null;
  const nodeIds = selected.value.currentNodeIds;
  if (nodeIds.length === 0) return null;

  const tokens = selected.value.bpmnTokens;
  const activeTokens = nodeIds
    .map((id) => tokens[id])
    .filter((t): t is NonNullable<typeof t> => t !== undefined && t.status === BpmnTokenStatus.Active);

  return {
    count: activeTokens.length,
    names: activeTokens.map(t => t.nodeName).join(', '),
    nodeIds: nodeIds,
  };
});

const activeNodeOptions = computed(() => {
  if (!selected.value) return [];

  return selected.value.currentNodeIds
    .map((nodeId) => {
      const token = selected.value?.bpmnTokens[nodeId];
      return {
        nodeId,
        nodeName: token?.nodeName || nodeId,
      };
    })
    .filter((item) => selected.value?.bpmnTokens[item.nodeId]?.status === BpmnTokenStatus.Active);
});

const currentSignStates = computed(() => {
  if (!selected.value) return [];

  return selected.value.currentNodeIds.flatMap((nodeId) => {
    const token = selected.value?.bpmnTokens[nodeId];
    if (!token?.signStates) return [];

    return Object.entries(token.signStates).map(([name, signed]) => ({
      name,
      nodeId,
      nodeName: token.nodeName,
      signed,
    }));
  });
});

function resolveNodeId() {
  if (!selected.value) return undefined;
  if (selected.value.currentNodeIds.length <= 1) {
    return selected.value.currentNodeIds[0];
  }

  return selectedNodeId.value || undefined;
}

function ensureNodeSelected() {
  if (selected.value && selected.value.currentNodeIds.length > 1 && !selectedNodeId.value) {
    ElMessage.warning('请选择要处理的并行节点');
    return false;
  }

  return true;
}

function openAddSign() {
  if (!selected.value) return;
  if (!ensureNodeSelected()) return;
  addSignUser.value = '';
  addSignVisible.value = true;
}

async function addSign() {
  if (!selected.value) return;
  if (!addSignUser.value) {
    ElMessage.warning('请选择加签人');
    return;
  }
  if (!ensureNodeSelected()) return;

  addSignLoading.value = true;
  try {
    const updated = await addSignFlowApi(selected.value.id, {
      nodeId: resolveNodeId(),
      who: addSignUser.value,
    });
    selected.value = updated;
    const index = flows.value.findIndex((item) => item.id === updated.id);
    if (index >= 0) flows.value[index] = updated;
    ElMessage.success('已加签');
    addSignVisible.value = false;
  } catch (error: any) {
    ElMessage.error(error.message || '加签失败');
  } finally {
    addSignLoading.value = false;
  }
}

async function approve() {
  if (!selected.value) return;
  actionLoading.value = true;
  try {
    const payload: any = { opinion: opinion.value };
    const nodeId = resolveNodeId();

    if (selected.value.currentNodeIds.length > 1 && nodeId) {
      payload.nodeId = nodeId;
    } else if (selected.value.currentNodeIds.length > 1) {
      ElMessage.warning('请选择要处理的并行节点');
      return;
    }

    await approveFlowApi(selected.value.id, payload);
    ElMessage.success('已通过');
    detailVisible.value = false;
    await loadData();
  } catch (error: any) {
    ElMessage.error(error.message || '审批失败');
  } finally {
    actionLoading.value = false;
  }
}

async function reject() {
  if (!selected.value || !opinion.value.trim()) {
    ElMessage.warning('请填写驳回理由');
    return;
  }
  if (!ensureNodeSelected()) return;
  actionLoading.value = true;
  try {
    await rejectFlowApi(selected.value.id, { nodeId: resolveNodeId(), reason: opinion.value });
    ElMessage.success('已驳回');
    detailVisible.value = false;
    await loadData();
  } catch (error: any) {
    ElMessage.error(error.message || '驳回失败');
  } finally {
    actionLoading.value = false;
  }
}

// 防抖版本的审批/驳回方法,防止用户快速点击导致重复提交
const debouncedApprove = useDebounceFn(approve, 300);
const debouncedReject = useDebounceFn(reject, 300);

function getBizTypeLabel(type: string) {
  const map: Record<string, string> = {
    borrow: '借用',
    transfer: '转让',
    return: '归还',
  };
  return map[type] || type;
}

onMounted(() => {
  loadData();
});
</script>

<template>
  <re-page>
    <div class="pending-page">
      <div class="pending-header">
        <div>
          <h2 class="pending-title">待我审批</h2>
          <p class="pending-subtitle">需要我审批的待办事项</p>
        </div>
        <ElButton type="primary" @click="loadData" :loading="loading">刷新</ElButton>
      </div>

      <div class="pending-table-panel">
        <ElTable :data="flows" v-loading="loading" border>
          <ElTableColumn prop="flowNo" label="流程单号" width="180" />
          <ElTableColumn prop="bizType" label="业务类型" width="120" align="center">
            <template #default="{ row }">
              <ElTag v-if="row.bizType === 'borrow'" type="success" size="small">
                {{ getBizTypeLabel(row.bizType) }}
              </ElTag>
              <ElTag v-else-if="row.bizType === 'transfer'" type="warning" size="small">
                {{ getBizTypeLabel(row.bizType) }}
              </ElTag>
              <ElTag v-else type="info" size="small">
                {{ getBizTypeLabel(row.bizType) }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn prop="assetName" label="资产名称" min-width="160" />
          <ElTableColumn class-name="hide-on-mobile" prop="applicant" label="申请人" width="120" />
          <ElTableColumn class-name="hide-on-mobile" prop="applyTime" label="申请时间" width="170" />
          <ElTableColumn label="当前节点" width="160">
            <template #default="{ row }">
              <span v-if="row.currentNodeIds.length === 1">
                {{ row.bpmnTokens[row.currentNodeIds[0]]?.nodeName || '-' }}
              </span>
              <ElTag v-else type="info" size="small">
                {{ row.currentNodeIds.length }} 个并行节点
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="操作" width="120" fixed="right" align="center">
            <template #default="{ row }">
              <ElButton type="primary" link size="small" @click="openDetail(row)">
                审批
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>

      <!-- 审批对话框 -->
      <ElDialog
        v-model="detailVisible"
        title="审批"
        width="640px"
        :close-on-click-modal="false"
      >
        <ElDescriptions v-if="selected" :column="2" border>
          <ElDescriptionsItem label="流程单号">{{ selected.flowNo }}</ElDescriptionsItem>
          <ElDescriptionsItem label="业务类型">
            {{ getBizTypeLabel(selected.bizType) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="资产编号">{{ selected.assetNo }}</ElDescriptionsItem>
          <ElDescriptionsItem label="资产名称">{{ selected.assetName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="申请人">{{ selected.applicant }}</ElDescriptionsItem>
          <ElDescriptionsItem label="申请部门">{{ selected.applicantDept || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="申请时间" :span="2">
            {{ selected.applyTime }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="申请理由" :span="2">
            {{ selected.reason || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="selected.bizType === 'borrow'" label="归还日期" :span="2">
            {{ selected.returnDate || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="currentNodeInfo" label="当前节点" :span="2">
            {{ currentNodeInfo.names }}
            <span v-if="currentNodeInfo.count > 1" class="pending-node-tip">
              ({{ currentNodeInfo.count }} 个并行节点)
            </span>
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="activeNodeOptions.length > 1" label="处理节点" :span="2">
            <ElSelect
              v-model="selectedNodeId"
              placeholder="请选择要处理的节点"
              style="width: 100%"
            >
              <ElOption
                v-for="item in activeNodeOptions"
                :key="item.nodeId"
                :label="item.nodeName"
                :value="item.nodeId"
              />
            </ElSelect>
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="currentSignStates.length > 0" label="签核状态" :span="2">
            <div class="pending-sign-list">
              <ElTag
                v-for="item in currentSignStates"
                :key="`${item.nodeId}-${item.name}`"
                :type="item.signed ? 'success' : 'warning'"
                size="small"
              >
                {{ item.name }} {{ item.signed ? '已签' : '待签' }}
              </ElTag>
            </div>
          </ElDescriptionsItem>
        </ElDescriptions>

        <div class="pending-opinion-panel">
          <ElInput
            v-model="opinion"
            type="textarea"
            :rows="3"
            placeholder="请输入审批意见"
          />
        </div>

        <template #footer>
          <ElButton @click="detailVisible = false">取消</ElButton>
          <ElButton @click="openAddSign" :loading="addSignLoading">
            加签
          </ElButton>
          <ElButton type="danger" @click="debouncedReject" :loading="actionLoading">
            驳回
          </ElButton>
          <ElButton type="primary" @click="debouncedApprove" :loading="actionLoading">
            通过
          </ElButton>
        </template>
      </ElDialog>

      <!-- 加签对话框 -->
      <ElDialog
        v-model="addSignVisible"
        title="加签"
        width="460px"
        :close-on-click-modal="false"
      >
        <ElSelect
          v-model="addSignUser"
          filterable
          placeholder="选择加签人"
          style="width: 100%"
        >
          <ElOption
            v-for="user in users"
            :key="user.id"
            :label="`${user.name}（${user.employeeNo}）`"
            :value="user.name"
          />
        </ElSelect>

        <template #footer>
          <ElButton @click="addSignVisible = false">取消</ElButton>
          <ElButton type="primary" :loading="addSignLoading" @click="addSign">
            确认加签
          </ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
/* ========== 设计系统规范 ========== */
.pending-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
  min-height: calc(100vh - 112px);
}

/* ========== 页面头部 ========== */
.pending-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
  box-shadow: var(--asset-page-shadow);
}

.pending-title {
  margin: 0 0 4px 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: var(--asset-page-text);
  letter-spacing: -0.02em;
}

.pending-subtitle {
  margin: 0;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

/* ========== 表格面板 ========== */
.pending-table-panel {
  flex: 1;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  overflow: hidden;
}

.pending-table-panel :deep(.el-table) {
  font-size: 14px;
  line-height: 20px;
}

.pending-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.pending-table-panel :deep(.el-table--border) {
  border: none;
}

.pending-table-panel :deep(.el-table td.el-table__cell),
.pending-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.pending-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

/* ========== 审批详情 ========== */
.pending-node-tip {
  margin-left: 8px;
  font-size: 12px;
  line-height: 16px;
  color: var(--asset-page-muted);
}

.pending-sign-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.pending-opinion-panel {
  margin-top: 20px;
}

/* ========== 对话框优化 ========== */
:deep(.el-dialog) {
  border-radius: 12px;
}

:deep(.el-dialog__header) {
  padding: 20px 24px;
  border-bottom: 1px solid var(--asset-page-border);
}

:deep(.el-dialog__body) {
  padding: 24px;
}

:deep(.el-dialog__footer) {
  padding: 16px 24px;
  border-top: 1px solid var(--asset-page-border);
}

:deep(.el-descriptions) {
  font-size: 14px;
  line-height: 20px;
}

:deep(.el-descriptions__label) {
  font-weight: 500;
  color: var(--asset-page-text-secondary);
}

:deep(.el-descriptions__content) {
  color: var(--asset-page-text);
}

:deep(.el-input__inner) {
  font-size: 14px;
  line-height: 20px;
}

:deep(.el-textarea__inner) {
  font-size: 14px;
  line-height: 20px;
}
</style>
