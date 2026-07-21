<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';

import type { MaterialFlowItem } from '#/api/material';
import type { UserOptionDto } from '#/api/user';
import type { ApprovalActionPayload, ApprovalFlow } from '#/api/workflow';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { useDebounceFn } from '@vueuse/core';
import {
  ElButton,
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElRadioButton,
  ElRadioGroup,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTabPane,
  ElTabs,
  ElTag,
} from 'element-plus';

import {
  approveFlowApi as approveMaterialFlowApi,
  listHandledFlowsPageApi,
  listPendingFlowsPageApi,
  rejectFlowApi as rejectMaterialFlowApi,
} from '#/api/material';
import { getApproverOptionsApi } from '#/api/user';
import {
  addSignFlowApi,
  approveFlowApi,
  BpmnTokenStatus,
  cancelAddSignFlowApi,
  getHandledApprovalsPageApi,
  getPendingApprovalsPageApi,
  rejectFlowApi,
} from '#/api/workflow';
import WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';
import WorkflowProgressDetail from '#/components/workflow/WorkflowProgressDetail.vue';
import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';
import { formatDate, formatDateTime } from '#/utils/date-format';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { formatWorkflowNode } from '#/utils/workflow-action-nodes';

import {
  canUserInitiateAddSign,
  excludeCurrentUserFromAddSignCandidates,
} from '../add-sign-access';
import {
  findApprovalWorkItemIndex,
  normalizeAssetApproval,
  normalizeMaterialFlow,
} from '../approval-work-items';
import {
  type ApprovalSource,
  approvalListRequestFlowId,
  beginNotificationFlowAttempt,
  notificationSource,
  withoutNotificationFlowId,
} from '../notification-flow-attempt';

defineOptions({ name: 'ApprovalPending' });

const { hasAccessByCodes } = useAccess();
const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const currentUserId = computed(() => Number(userStore.userInfo?.userId || 0));
const canAddSign = computed(() => hasAccessByCodes(['approval:add-sign']));
const canHandleMaterialFlow = computed(() =>
  hasAccessByCodes(['material-flow:approve']),
);
const loading = ref(false);
const actionLoading = ref(false);
const addSignLoading = ref(false);
const cancelAddSignLoadingKey = ref('');
const detailVisible = ref(false);
const addSignVisible = ref(false);
const selected = ref<ApprovalFlow | null>(null);
const handledSelected = ref<ApprovalWorkItem | null>(null);
const handledDetailVisible = ref(false);
const activeSource = ref<ApprovalSource>('asset');
const viewMode = ref<'handled' | 'pending'>('pending');
const assetFlows = ref<ApprovalWorkItem[]>([]);
const materialFlows = ref<ApprovalWorkItem[]>([]);
const assetTotal = ref(0);
const materialTotal = ref(0);
const users = ref<UserOptionDto[]>([]);
const materialActionLoadingIds = ref(new Set<number>());
const pageSizeOptions = ref(createPageSizeOptions(20));
const opinion = ref('同意');
const addSignUser = ref('');
const selectedNodeId = ref('');
const workflowNodeSelector = ref<InstanceType<
  typeof WorkflowNodeSelectDialog
> | null>(null);
const openedNotificationFlowKey = ref('');
const listRequestGuard = createLatestRequestGuard();
const query = reactive({
  keyword: '',
  bizType: '',
});
const assetPage = reactive({ page: 1, pageSize: 20 });
const materialPage = reactive({ page: 1, pageSize: 20 });
const currentPage = computed(() =>
  activeSource.value === 'asset' ? assetPage : materialPage,
);
const flows = computed(() =>
  activeSource.value === 'asset' ? assetFlows.value : materialFlows.value,
);
const currentTotal = computed(() =>
  activeSource.value === 'asset' ? assetTotal.value : materialTotal.value,
);
const addSignCandidates = computed(() =>
  excludeCurrentUserFromAddSignCandidates(users.value, currentUserId.value),
);

async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  let requestedNotificationKey = '';
  try {
    const page = currentPage.value;
    const notificationFlowId = Number(route.query.flowId || 0);
    const targetSource = notificationSource(route.query.source, route.path);
    const attempt = beginNotificationFlowAttempt(
      notificationFlowId,
      targetSource,
      activeSource.value,
      openedNotificationFlowKey.value,
    );
    const notificationKey = attempt.key;
    const requestedFlowId = approvalListRequestFlowId(
      viewMode.value,
      attempt.requestedFlowId,
    );
    if (requestedFlowId) {
      requestedNotificationKey = notificationKey;
      // 深链只尝试一次；即使目标已过期或无权限，也不能让 flowId 永久约束列表。
      openedNotificationFlowKey.value = attempt.consumedKey;
    }
    if (activeSource.value === 'asset') {
      const request =
        viewMode.value === 'pending'
          ? getPendingApprovalsPageApi
          : getHandledApprovalsPageApi;
      const result = await request({
        bizType: requestedFlowId ? undefined : query.bizType || undefined,
        flowId: requestedFlowId,
        keyword: requestedFlowId
          ? undefined
          : query.keyword.trim() || undefined,
        page: requestedFlowId ? 1 : page.page,
        pageSize: page.pageSize,
      });
      if (!listRequestGuard.isLatest(requestGeneration)) return;
      const lastPage = Math.max(1, Math.ceil(result.total / page.pageSize));
      if (page.page > lastPage) {
        page.page = lastPage;
        await loadData();
        return;
      }
      assetFlows.value = result.items.map((flow) =>
        normalizeAssetApproval(flow),
      );
      assetTotal.value = result.total;
    } else if (canHandleMaterialFlow.value) {
      const request =
        viewMode.value === 'pending'
          ? listPendingFlowsPageApi
          : listHandledFlowsPageApi;
      const result = await request({
        flowId: requestedFlowId,
        keyword: requestedFlowId
          ? undefined
          : query.keyword.trim() || undefined,
        page: requestedFlowId ? 1 : page.page,
        pageSize: page.pageSize,
      });
      if (!listRequestGuard.isLatest(requestGeneration)) return;
      const lastPage = Math.max(1, Math.ceil(result.total / page.pageSize));
      if (page.page > lastPage) {
        page.page = lastPage;
        await loadData();
        return;
      }
      materialFlows.value = result.items.map((flow) =>
        normalizeMaterialFlow(flow),
      );
      materialTotal.value = result.total;
    }
    if (canAddSign.value && users.value.length === 0) {
      users.value = await getApproverOptionsApi();
      if (!listRequestGuard.isLatest(requestGeneration)) return;
    }
    if (requestedFlowId) {
      const target = flows.value.find(
        (item) =>
          item.source === targetSource && item.id === notificationFlowId,
      );
      if (target) {
        if (target.source === 'asset') {
          openDetail(target);
        }
      } else {
        ElMessage.warning('该通知对应的待办已处理、已过期或当前无权查看');
      }
      clearNotificationFlowQuery();
    }
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
    if (requestedNotificationKey) clearNotificationFlowQuery();
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

function clearNotificationFlowQuery() {
  if (!route.query.flowId) return;
  const nextQuery = withoutNotificationFlowId(route.query);
  runHandled(router.replace({ path: route.path, query: nextQuery }));
}

watch(
  () => [route.query.flowId, route.query.source],
  () => {
    activeSource.value = notificationSource(route.query.source, route.path);
    currentPage.value.page = 1;
    runHandled(loadData());
  },
);

function openDetail(item: ApprovalWorkItem) {
  if (item.source !== 'asset') return;
  const flow = item.raw as ApprovalFlow;
  selected.value = flow;
  opinion.value = '同意';
  addSignUser.value = '';
  selectedNodeId.value = flow.actionableNodeIds[0] || '';
  detailVisible.value = true;
}

function openHandledDetail(item: ApprovalWorkItem) {
  handledSelected.value = item;
  handledDetailVisible.value = true;
}

// 获取当前活跃节点信息
const currentNodeInfo = computed(() => {
  if (!selected.value) return null;
  const nodeIds = selected.value.actionableNodeIds;
  if (nodeIds.length === 0) return null;

  const tokens = selected.value.bpmnTokens;
  const activeTokens = nodeIds
    .map((id) => tokens[id])
    .filter(
      (t): t is NonNullable<typeof t> =>
        t !== undefined && t.status === BpmnTokenStatus.Active,
    );

  return {
    count: activeTokens.length,
    names: activeTokens.map((t) => t.nodeName).join(', '),
    nodeIds,
  };
});

const activeNodeOptions = computed(() => {
  if (!selected.value) return [];

  return selected.value.actionableNodeIds
    .map((nodeId) => {
      const token = selected.value?.bpmnTokens[nodeId];
      return {
        nodeId,
        nodeName: token?.nodeName || nodeId,
      };
    })
    .filter(
      (item) =>
        selected.value?.bpmnTokens[item.nodeId]?.status ===
        BpmnTokenStatus.Active,
    );
});

const currentSignStates = computed(() => {
  if (!selected.value) return [];

  return selected.value.actionableNodeIds.flatMap((nodeId) => {
    const token = selected.value?.bpmnTokens[nodeId];
    if (!token?.signStates) return [];

    return Object.entries(token.signStates).map(([name, signed]) => ({
      displayName:
        users.value.find((user) => String(user.id) === name)?.name || name,
      name,
      nodeId,
      nodeName: token.nodeName,
      signed,
      canCancel: !signed && token.addedSigners?.[name] === currentUserId.value,
    }));
  });
});

function resolveNodeId() {
  if (!selected.value) return undefined;
  if (selected.value.actionableNodeIds.length <= 1) {
    return selected.value.actionableNodeIds[0];
  }

  return selected.value.actionableNodeIds.includes(selectedNodeId.value)
    ? selectedNodeId.value
    : undefined;
}

const canCurrentUserAddSign = computed(() => {
  if (!canAddSign.value || !selected.value) return false;
  const nodeId = resolveNodeId();
  if (!nodeId) return false;

  return canUserInitiateAddSign(
    selected.value.bpmnTokens[nodeId],
    currentUserId.value,
  );
});

async function cancelAddSign(item: {
  displayName: string;
  name: string;
  nodeId: string;
}) {
  if (!selected.value) return;
  try {
    await ElMessageBox.confirm(
      `确认取消对“${item.displayName}”的加签吗？`,
      '取消加签',
      {
        type: 'warning',
        confirmButtonText: '确认取消',
        cancelButtonText: '返回',
      },
    );
  } catch {
    return;
  }
  cancelAddSignLoadingKey.value = `${item.nodeId}-${item.name}`;
  try {
    const updated = await cancelAddSignFlowApi(selected.value.id, {
      nodeId: item.nodeId,
      who: item.name,
    });
    selected.value = updated;
    const index = findApprovalWorkItemIndex(flows.value, 'asset', updated.id);
    if (index >= 0) flows.value[index] = normalizeAssetApproval(updated);
    ElMessage.success('已取消加签');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    cancelAddSignLoadingKey.value = '';
  }
}

function ensureNodeSelected() {
  if (!selected.value || selected.value.actionableNodeIds.length === 0) {
    ElMessage.warning('当前没有可操作的审批节点，请刷新待办列表');
    return false;
  }
  if (selected.value.actionableNodeIds.length > 1 && !resolveNodeId()) {
    ElMessage.warning('请选择要处理的并行节点');
    return false;
  }

  return true;
}

function openAddSign() {
  if (!selected.value) return;
  if (!canCurrentUserAddSign.value) {
    ElMessage.warning('被加签人不能再次发起加签');
    return;
  }
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
    const index = findApprovalWorkItemIndex(flows.value, 'asset', updated.id);
    if (index >= 0) flows.value[index] = normalizeAssetApproval(updated);
    ElMessage.success('已加签');
    addSignVisible.value = false;
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    addSignLoading.value = false;
  }
}

async function approve() {
  if (!selected.value) return;
  if (!ensureNodeSelected()) return;
  actionLoading.value = true;
  try {
    const nodeId = resolveNodeId();
    if (!nodeId) return;
    const payload: ApprovalActionPayload = { nodeId, opinion: opinion.value };
    await approveFlowApi(selected.value.id, payload);
    ElMessage.success('已通过');
    detailVisible.value = false;
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    actionLoading.value = false;
  }
}

async function reject() {
  if (!selected.value) {
    ElMessage.warning('请选择要驳回的记录');
    return;
  }
  if (!opinion.value.trim()) {
    ElMessage.warning('请填写驳回理由');
    return;
  }
  if (!ensureNodeSelected()) return;
  actionLoading.value = true;
  try {
    await rejectFlowApi(selected.value.id, {
      nodeId: resolveNodeId(),
      reason: opinion.value,
    });
    ElMessage.success('已驳回');
    detailVisible.value = false;
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    actionLoading.value = false;
  }
}

async function approveMaterial(item: ApprovalWorkItem) {
  const flow = item.raw as MaterialFlowItem;
  const node = await workflowNodeSelector.value?.selectNode(flow, '通过');
  if (!node) return;
  try {
    await ElMessageBox.confirm(
      `确认通过料件「${item.objectName}」的流转申请？处理节点：${formatWorkflowNode(node)}`,
      '审批通过',
      { type: 'warning' },
    );
  } catch {
    return;
  }

  materialActionLoadingIds.value.add(item.id);
  try {
    await approveMaterialFlowApi(item.id, '同意', node.id);
    ElMessage.success('已通过');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    materialActionLoadingIds.value.delete(item.id);
  }
}

async function rejectMaterial(item: ApprovalWorkItem) {
  const flow = item.raw as MaterialFlowItem;
  const node = await workflowNodeSelector.value?.selectNode(flow, '驳回');
  if (!node) return;
  let reason = '不同意';
  try {
    const result = await ElMessageBox.prompt(
      `请输入驳回原因。处理节点：${formatWorkflowNode(node)}`,
      '驳回',
      {
        inputPlaceholder: '驳回原因',
      },
    );
    reason = result.value || reason;
  } catch {
    return;
  }

  materialActionLoadingIds.value.add(item.id);
  try {
    await rejectMaterialFlowApi(item.id, reason, node.id);
    ElMessage.success('已驳回');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    materialActionLoadingIds.value.delete(item.id);
  }
}

// 防抖版本的审批/驳回方法,防止用户快速点击导致重复提交
const debouncedApprove = useDebounceFn(approve, 300);
const debouncedReject = useDebounceFn(reject, 300);

function onPageSizeChange() {
  currentPage.value.page = 1;
  runHandled(loadData());
}

function search() {
  currentPage.value.page = 1;
  runHandled(loadData());
}

function resetQuery() {
  query.keyword = '';
  query.bizType = '';
  currentPage.value.page = 1;
  runHandled(loadData());
}

function onSourceChange() {
  query.bizType = '';
  currentPage.value.page = 1;
  runHandled(loadData());
}

function onViewModeChange() {
  assetPage.page = 1;
  materialPage.page = 1;
  runHandled(loadData());
}

function approvalActionLabel(action: null | string | undefined) {
  return action === 'reject' ? '已驳回' : '已通过';
}

function flowStatusLabel(status: string) {
  return (
    {
      approved: '已通过',
      pending: '流转中',
      rejected: '已驳回',
      withdrawn: '已撤回',
    }[status] || status
  );
}

function getBizTypeLabel(type: string) {
  const map: Record<string, string> = {
    borrow: '借用',
    extension: '延期',
    transfer: '转让',
    return: '归还',
    material_transfer: '测试料件流转',
  };
  return map[type] || type;
}

onMounted(async () => {
  const pageSize = await getDefaultPageSize();
  assetPage.pageSize = pageSize;
  materialPage.pageSize = pageSize;
  pageSizeOptions.value = createPageSizeOptions(pageSize);
  activeSource.value = notificationSource(route.query.source, route.path);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="pending-page">
      <div class="pending-view-switch" aria-label="审批记录范围">
        <ElRadioGroup v-model="viewMode" @change="onViewModeChange">
          <ElRadioButton value="pending">待我处理</ElRadioButton>
          <ElRadioButton value="handled">我已处理</ElRadioButton>
        </ElRadioGroup>
        <span class="pending-view-tip">
          {{
            viewMode === 'pending'
              ? '仅显示当前需要您处理的审批任务'
              : '显示您已经通过或驳回的审批记录'
          }}
        </span>
      </div>

      <div class="filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="关键字">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="流程单号/资产/料件/申请人/节点"
              style="width: 260px"
              @keyup.enter="search"
            />
          </ElFormItem>
          <ElFormItem v-if="activeSource === 'asset'" label="业务类型">
            <ElSelect
              v-model="query.bizType"
              clearable
              placeholder="全部类型"
              style="width: 140px"
            >
              <ElOption label="借用" value="borrow" />
              <ElOption label="转让" value="transfer" />
              <ElOption label="延期" value="extension" />
              <ElOption label="归还" value="return" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <ElTabs v-model="activeSource" @tab-change="onSourceChange">
        <ElTabPane label="资产审批" name="asset" />
        <ElTabPane
          v-if="canHandleMaterialFlow"
          label="料件审批"
          name="material"
        />
      </ElTabs>

      <div class="pending-table-panel">
        <ElTable :data="flows" border height="100%" v-loading="loading">
          <ElTableColumn label="流程单号" prop="flowNo" width="180" />
          <ElTableColumn align="center" label="来源" width="110">
            <template #default="{ row }">
              <ElTag
                :type="row.source === 'asset' ? 'primary' : 'success'"
                size="small"
              >
                {{ row.sourceLabel }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            align="center"
            label="业务类型"
            prop="bizType"
            width="120"
          >
            <template #default="{ row }">
              <ElTag
                v-if="row.bizType === 'borrow'"
                size="small"
                type="success"
              >
                {{ row.typeLabel }}
              </ElTag>
              <ElTag
                v-else-if="row.bizType === 'transfer'"
                size="small"
                type="warning"
              >
                {{ row.typeLabel }}
              </ElTag>
              <ElTag
                v-else-if="row.bizType === 'material_transfer'"
                size="small"
                type="primary"
              >
                {{ row.typeLabel }}
              </ElTag>
              <ElTag v-else size="small" type="info">
                {{ row.typeLabel }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="对象名称" min-width="160" prop="objectName" />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="申请人"
            prop="applicant"
            width="120"
          />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="借用人/接收人"
            prop="participant"
            width="140"
          />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="申请时间"
            width="170"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.applyTime) }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="审批进度" min-width="320">
            <template #default="{ row }">
              <WorkflowProgressSummary
                :current-steps="row.raw.currentSteps"
                :next-steps="row.raw.nextSteps"
                :status="row.status"
              />
            </template>
          </ElTableColumn>
          <ElTableColumn
            v-if="viewMode === 'handled'"
            align="center"
            label="我的处理"
            width="110"
          >
            <template #default="{ row }">
              <ElTag
                :type="row.myApprovalAction === 'reject' ? 'danger' : 'success'"
                effect="plain"
                size="small"
              >
                {{ approvalActionLabel(row.myApprovalAction) }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            v-if="viewMode === 'handled'"
            class-name="hide-on-mobile"
            label="处理时间"
            width="170"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.myApprovalTime) }}
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" fixed="right" label="操作" width="150">
            <template #default="{ row }">
              <ElButton
                v-if="viewMode === 'handled'"
                link
                size="small"
                type="primary"
                @click="openHandledDetail(row)"
              >
                查看记录
              </ElButton>
              <ElButton
                v-else-if="row.source === 'asset'"
                link
                size="small"
                type="primary"
                @click="openDetail(row)"
              >
                审批
              </ElButton>
              <template v-else>
                <ElButton
                  :disabled="row.actionableNodeIds.length === 0"
                  :loading="materialActionLoadingIds.has(row.id)"
                  link
                  size="small"
                  type="success"
                  @click="approveMaterial(row)"
                >
                  通过
                </ElButton>
                <ElButton
                  :disabled="row.actionableNodeIds.length === 0"
                  :loading="materialActionLoadingIds.has(row.id)"
                  link
                  size="small"
                  type="danger"
                  @click="rejectMaterial(row)"
                >
                  驳回
                </ElButton>
              </template>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ currentTotal }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="currentPage.pageSize"
              style="width: 92px"
              @change="onPageSizeChange"
            >
              <ElOption
                v-for="size in pageSizeOptions"
                :key="size"
                :label="`${size}`"
                :value="size"
              />
            </ElSelect>
          </div>
          <ElPagination
            v-model:current-page="currentPage.page"
            :page-size="currentPage.pageSize"
            :total="currentTotal"
            background
            layout="prev, pager, next"
            @current-change="loadData"
          />
        </div>
      </div>

      <!-- 审批对话框 -->
      <ElDialog
        v-model="detailVisible"
        :close-on-click-modal="false"
        title="审批"
        width="640px"
      >
        <ElDescriptions v-if="selected" :column="2" border>
          <ElDescriptionsItem label="流程单号">
            {{ selected.flowNo }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="业务类型">
            {{ getBizTypeLabel(selected.bizType) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="资产编号">
            {{ selected.assetNo }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="资产名称">
            {{ selected.assetName }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="申请人">
            {{ selected.applicant }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="申请部门">
            {{ selected.applicantDept || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="申请时间">
            {{ formatDateTime(selected.applyTime) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="申请理由">
            {{ selected.reason || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem
            v-if="selected.bizType === 'borrow'"
            :span="2"
            label="归还日期"
          >
            {{ formatDate(selected.returnDate) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem
            v-if="selected.bizType === 'extension'"
            label="原应归还日期"
          >
            {{ formatDate(selected.originalReturnDate) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem
            v-if="selected.bizType === 'extension'"
            label="新归还日期"
          >
            {{ formatDate(selected.returnDate) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem v-if="currentNodeInfo" :span="2" label="当前节点">
            {{ currentNodeInfo.names }}
            <span v-if="currentNodeInfo.count > 1" class="pending-node-tip">
              ({{ currentNodeInfo.count }} 个并行节点)
            </span>
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="完整流转">
            <WorkflowProgressDetail :steps="selected.progressSteps || []" />
          </ElDescriptionsItem>
          <ElDescriptionsItem
            v-if="activeNodeOptions.length > 1"
            :span="2"
            label="处理节点"
          >
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
          <ElDescriptionsItem
            v-if="currentSignStates.length > 0"
            :span="2"
            label="签核状态"
          >
            <div class="pending-sign-list">
              <span
                v-for="item in currentSignStates"
                :key="`${item.nodeId}-${item.name}`"
                class="pending-sign-item"
              >
                <ElTag :type="item.signed ? 'success' : 'warning'" size="small">
                  {{ item.displayName }} {{ item.signed ? '已签' : '待签' }}
                </ElTag>
                <ElButton
                  v-if="item.canCancel"
                  :loading="
                    cancelAddSignLoadingKey === `${item.nodeId}-${item.name}`
                  "
                  link
                  size="small"
                  type="danger"
                  @click="cancelAddSign(item)"
                >
                  取消加签
                </ElButton>
              </span>
            </div>
          </ElDescriptionsItem>
        </ElDescriptions>

        <div class="pending-opinion-panel">
          <ElInput
            v-model="opinion"
            :rows="3"
            placeholder="请输入审批意见"
            type="textarea"
          />
        </div>

        <template #footer>
          <ElButton @click="detailVisible = false">取消</ElButton>
          <ElButton
            v-if="canCurrentUserAddSign"
            :loading="addSignLoading"
            @click="openAddSign"
          >
            加签
          </ElButton>
          <ElButton
            :loading="actionLoading"
            type="danger"
            @click="debouncedReject"
          >
            驳回
          </ElButton>
          <ElButton
            :loading="actionLoading"
            type="primary"
            @click="debouncedApprove"
          >
            通过
          </ElButton>
        </template>
      </ElDialog>

      <ElDialog v-model="handledDetailVisible" title="审批记录" width="680px">
        <ElDescriptions v-if="handledSelected" :column="2" border>
          <ElDescriptionsItem label="流程单号">
            {{ handledSelected.flowNo }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="当前状态">
            {{ flowStatusLabel(handledSelected.status) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="业务类型">
            {{ handledSelected.typeLabel }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="审批对象">
            {{ handledSelected.objectName }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="申请人">
            {{ handledSelected.applicant }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="我的处理">
            <ElTag
              :type="
                handledSelected.myApprovalAction === 'reject'
                  ? 'danger'
                  : 'success'
              "
              effect="plain"
              size="small"
            >
              {{ approvalActionLabel(handledSelected.myApprovalAction) }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="申请时间">
            {{ formatDateTime(handledSelected.applyTime) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="处理时间">
            {{ formatDateTime(handledSelected.myApprovalTime) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="申请理由">
            {{ handledSelected.reason || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem :span="2" label="完整审批记录">
            <WorkflowProgressDetail
              :steps="handledSelected.raw.progressSteps || []"
            />
          </ElDescriptionsItem>
        </ElDescriptions>
        <template #footer>
          <ElButton @click="handledDetailVisible = false">关闭</ElButton>
        </template>
      </ElDialog>

      <!-- 加签对话框 -->
      <ElDialog
        v-model="addSignVisible"
        :close-on-click-modal="false"
        title="加签"
        width="460px"
      >
        <ElSelect
          v-model="addSignUser"
          filterable
          placeholder="选择部门主管"
          style="width: 100%"
        >
          <ElOption
            v-for="user in addSignCandidates"
            :key="user.id"
            :label="`${user.name}（${user.employeeNo}）`"
            :value="String(user.id)"
          />
        </ElSelect>

        <template #footer>
          <ElButton @click="addSignVisible = false">取消</ElButton>
          <ElButton :loading="addSignLoading" type="primary" @click="addSign">
            确认加签
          </ElButton>
        </template>
      </ElDialog>

      <WorkflowNodeSelectDialog ref="workflowNodeSelector" />
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
}

.pending-view-switch {
  display: flex;
  align-items: center;
  gap: 16px;
}

.pending-view-tip {
  font-size: 13px;
  color: var(--asset-page-muted);
}

/* ========== 表格面板 ========== */
.pending-table-panel {
  flex: 1;
  display: flex;
  min-height: 0;
  flex-direction: column;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  overflow: hidden;
}

.pending-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
  font-size: 14px;
  line-height: 20px;
}

.pending-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-panel-header-solid);
  color: var(--asset-page-panel-header-text);
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

.pending-sign-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
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

@media (max-width: 640px) {
  .pending-view-switch {
    align-items: flex-start;
    flex-direction: column;
    gap: 8px;
  }
}
</style>
