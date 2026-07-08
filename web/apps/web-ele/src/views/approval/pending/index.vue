<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';
import type { ApprovalFlow } from '#/api/workflow';
import type { UserDto } from '#/api/user';
import { computed, onMounted, reactive, ref } from 'vue';
import { useDebounceFn } from '@vueuse/core';
import { useAccess } from '@vben/access';
import {
  approveFlowApi as approveMaterialFlowApi,
  listPendingFlowsApi,
  rejectFlowApi as rejectMaterialFlowApi,
} from '#/api/material';
import {
  addSignFlowApi,
  approveFlowApi,
  BpmnTokenStatus,
  getPendingApprovalsApi,
  rejectFlowApi,
} from '#/api/workflow';
import { getUserListApi } from '#/api/user';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';
import { mergeApprovalWorkItems, normalizeAssetApproval } from '../approval-work-items';
import {
  ElButton,
  ElDialog,
  ElDescriptions,
  ElDescriptionsItem,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
  ElMessage,
  ElMessageBox,
} from 'element-plus';

defineOptions({ name: 'ApprovalPending' });

const { hasAccessByCodes } = useAccess();
const canAddSign = computed(() => hasAccessByCodes(['approval:add-sign']));
const canHandleMaterialFlow = computed(() => hasAccessByCodes(['material-flow:approve']));
const loading = ref(false);
const actionLoading = ref(false);
const addSignLoading = ref(false);
const detailVisible = ref(false);
const addSignVisible = ref(false);
const selected = ref<ApprovalFlow | null>(null);
const flows = ref<ApprovalWorkItem[]>([]);
const users = ref<UserDto[]>([]);
const materialActionLoadingIds = ref(new Set<number>());
const pageSizeOptions = ref(createPageSizeOptions(20));
const opinion = ref('同意');
const addSignUser = ref('');
const selectedNodeId = ref('');
const query = reactive({
  keyword: '',
  bizType: '',
  page: 1,
  pageSize: 20,
});

const filteredFlows = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();

  return flows.value.filter((flow) => {
    const matchBizType = !query.bizType || flow.bizType === query.bizType;
    const matchKeyword = !keyword
      || [
        flow.flowNo,
        flow.objectNo,
        flow.objectName,
        flow.applicant,
        flow.applicantDept,
        flow.transferee,
        flow.currentNodeLabel,
      ]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(keyword));

    return matchBizType && matchKeyword;
  });
});

const pagedFlows = computed(() => {
  const start = (query.page - 1) * query.pageSize;
  return filteredFlows.value.slice(start, start + query.pageSize);
});

async function loadData() {
  loading.value = true;
  try {
    const materialPendingPromise = canHandleMaterialFlow.value
      ? listPendingFlowsApi()
      : Promise.resolve([]);
    const [pending, materialPending, userPage] = await Promise.all([
      getPendingApprovalsApi(),
      materialPendingPromise,
      getUserListApi('', 1, 200),
    ]);
    flows.value = mergeApprovalWorkItems(pending, materialPending);
    if ((query.page - 1) * query.pageSize >= flows.value.length) {
      query.page = 1;
    }
    users.value = userPage.items.filter((user) => user.isActive);
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    loading.value = false;
  }
}

function openDetail(item: ApprovalWorkItem) {
  if (item.source !== 'asset') return;
  const flow = item.raw as ApprovalFlow;
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
  actionLoading.value = true;
  try {
    const payload: any = { opinion: opinion.value };
    const nodeId = resolveNodeId();

    if (selected.value.currentNodeIds.length > 1 && nodeId) {
      payload.nodeId = nodeId;
    } else if (selected.value.currentNodeIds.length > 1) {
      actionLoading.value = false;
      ElMessage.warning('请选择要处理的并行节点');
      return;
    }

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
    await rejectFlowApi(selected.value.id, { nodeId: resolveNodeId(), reason: opinion.value });
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
  try {
    await ElMessageBox.confirm(
      `确认通过料件「${item.objectName}」的流转申请？`,
      '审批通过',
      { type: 'warning' },
    );
  } catch {
    return;
  }

  materialActionLoadingIds.value.add(item.id);
  try {
    await approveMaterialFlowApi(item.id, '同意');
    ElMessage.success('已通过');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    materialActionLoadingIds.value.delete(item.id);
  }
}

async function rejectMaterial(item: ApprovalWorkItem) {
  let reason = '不同意';
  try {
    const result = await ElMessageBox.prompt('请输入驳回原因', '驳回', {
      inputPlaceholder: '驳回原因',
    });
    reason = result.value || reason;
  } catch {
    return;
  }

  materialActionLoadingIds.value.add(item.id);
  try {
    await rejectMaterialFlowApi(item.id, reason);
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
  query.page = 1;
}

function search() {
  query.page = 1;
}

function resetQuery() {
  query.keyword = '';
  query.bizType = '';
  query.page = 1;
}

function getBizTypeLabel(type: string) {
  const map: Record<string, string> = {
    borrow: '借用',
    transfer: '转让',
    return: '归还',
    material_transfer: '测试料件流转',
  };
  return map[type] || type;
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="pending-page">
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
          <ElFormItem label="业务类型">
            <ElSelect v-model="query.bizType" clearable placeholder="全部类型" style="width: 140px">
              <ElOption label="借用" value="borrow" />
              <ElOption label="转让" value="transfer" />
              <ElOption label="归还" value="return" />
              <ElOption label="测试料件流转" value="material_transfer" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="pending-table-panel">
        <ElTable :data="pagedFlows" v-loading="loading" border height="100%">
          <ElTableColumn prop="flowNo" label="流程单号" width="180" />
          <ElTableColumn label="来源" width="110" align="center">
            <template #default="{ row }">
              <ElTag :type="row.source === 'asset' ? 'primary' : 'success'" size="small">
                {{ row.sourceLabel }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn prop="bizType" label="业务类型" width="120" align="center">
            <template #default="{ row }">
              <ElTag v-if="row.bizType === 'borrow'" type="success" size="small">
                {{ row.typeLabel }}
              </ElTag>
              <ElTag v-else-if="row.bizType === 'transfer'" type="warning" size="small">
                {{ row.typeLabel }}
              </ElTag>
              <ElTag v-else-if="row.bizType === 'material_transfer'" type="primary" size="small">
                {{ row.typeLabel }}
              </ElTag>
              <ElTag v-else type="info" size="small">
                {{ row.typeLabel }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn prop="objectName" label="对象名称" min-width="160" />
          <ElTableColumn class-name="hide-on-mobile" prop="applicant" label="申请人" width="120" />
          <ElTableColumn class-name="hide-on-mobile" prop="transferee" label="接收人" width="120" />
          <ElTableColumn class-name="hide-on-mobile" prop="applyTime" label="申请时间" width="170" />
          <ElTableColumn label="当前节点" width="160">
            <template #default="{ row }">
              {{ row.currentNodeLabel }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="操作" width="150" fixed="right" align="center">
            <template #default="{ row }">
              <ElButton v-if="row.source === 'asset'" type="primary" link size="small" @click="openDetail(row)">
                审批
              </ElButton>
              <template v-else>
                <ElButton
                  :loading="materialActionLoadingIds.has(row.id)"
                  link
                  size="small"
                  type="success"
                  @click="approveMaterial(row)"
                >
                  通过
                </ElButton>
                <ElButton
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
            <span>共 {{ filteredFlows.length }} 条记录</span>
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
            :total="filteredFlows.length"
            background
            layout="prev, pager, next"
          />
        </div>
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
          <ElButton v-if="canAddSign" @click="openAddSign" :loading="addSignLoading">
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
