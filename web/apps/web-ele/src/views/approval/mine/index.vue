<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';

import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTabPane,
  ElTabs,
  ElTag,
} from 'element-plus';

import { getAssetListApi } from '#/api/asset';
import { listMyFlowsPageApi, withdrawMaterialFlowApi } from '#/api/material';
import { getUserOptionsPageApi } from '#/api/user';
import {
  getMineApprovalsPageApi,
  startApprovalApi,
  withdrawApprovalApi,
} from '#/api/workflow';
import WorkflowProgressDetail from '#/components/workflow/WorkflowProgressDetail.vue';
import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import {
  disableNonExtensionReturnDate,
  disableNonFutureReturnDate,
  isFutureReturnDate,
  isValidExtensionReturnDate,
} from '#/utils/return-date';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { mergeUserOptions } from '#/utils/user-options';
import { buildApprovalActionAccess } from '#/views/permissions/action-access';

import {
  canWithdrawApproval,
  normalizeAssetApproval,
  normalizeMaterialFlow,
} from '../approval-work-items';
import {
  type ApprovalSource,
  beginNotificationFlowAttempt,
  notificationSource,
  withoutNotificationFlowId,
} from '../notification-flow-attempt';
import {
  buildApprovalAssetQuery,
  getApprovalAssetSelectCopy,
  mergeApprovalAssetOptions,
} from './approval-asset-options';

defineOptions({ name: 'ApprovalMine' });

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const { hasAccessByCodes } = useAccess();
const approvalActionAccess = computed(() =>
  buildApprovalActionAccess(hasAccessByCodes),
);
const canViewMaterialFlow = computed(() =>
  hasAccessByCodes(['material-flow:view']),
);
const loading = ref(false);
const saving = ref(false);
const withdrawingKeys = ref<string[]>([]);
const dialogVisible = ref(false);
const progressVisible = ref(false);
const selectedFlow = ref<ApprovalWorkItem>();
const openedNotificationFlowKey = ref('');
const listRequestGuard = createLatestRequestGuard();
const activeSource = ref<ApprovalSource>('asset');
const assetFlows = ref<ApprovalWorkItem[]>([]);
const materialFlows = ref<ApprovalWorkItem[]>([]);
const assetTotal = ref(0);
const materialTotal = ref(0);
const assets = ref<AssetItem[]>([]);
const assetOptionsLoading = ref(false);
const assetSearchKeyword = ref('');
const assetOptionsRequestGuard = createLatestRequestGuard();
const users = ref<UserOptionDto[]>([]);
const userOptionsLoading = ref(false);
const userOptionsRequestGuard = createLatestRequestGuard();
const pageSizeOptions = ref(createPageSizeOptions(20));
const form = reactive({
  assetId: undefined as number | undefined,
  bizType: 'borrow',
  reason: '',
  returnDate: '',
  transfereeId: undefined as number | undefined,
});
const query = reactive({
  bizType: '',
  keyword: '',
  status: '',
});
const showReturnDate = computed(() =>
  ['borrow', 'extension'].includes(form.bizType),
);
const assetSelectCopy = computed(() =>
  getApprovalAssetSelectCopy(form.bizType, assetSearchKeyword.value),
);
const selectedAsset = computed(() =>
  assets.value.find((asset) => asset.id === form.assetId),
);
const inheritedReturnDate = computed(
  () => selectedAsset.value?.returnDate || '无（当前资产未借出）',
);

function disableReturnDate(time: Date) {
  return form.bizType === 'extension'
    ? disableNonExtensionReturnDate(time, selectedAsset.value?.returnDate || '')
    : disableNonFutureReturnDate(time);
}
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
    const flowId = attempt.requestedFlowId;
    if (flowId) {
      requestedNotificationKey = notificationKey;
      openedNotificationFlowKey.value = attempt.consumedKey;
    }
    if (activeSource.value === 'asset') {
      const result = await getMineApprovalsPageApi({
        bizType: flowId ? undefined : query.bizType || undefined,
        flowId: route.query.source === 'material' ? undefined : flowId,
        keyword: flowId ? undefined : query.keyword.trim() || undefined,
        page: flowId ? 1 : page.page,
        pageSize: page.pageSize,
        status: flowId ? undefined : query.status || undefined,
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
    } else if (canViewMaterialFlow.value) {
      const result = await listMyFlowsPageApi({
        flowId,
        keyword: flowId ? undefined : query.keyword.trim() || undefined,
        page: flowId ? 1 : page.page,
        pageSize: page.pageSize,
        status: flowId ? undefined : query.status || undefined,
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
    if (flowId) {
      if (!focusNotificationFlow()) {
        ElMessage.warning('该通知对应的申请已过期或当前无权查看');
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

async function openStart(type = 'borrow') {
  Object.assign(form, {
    assetId: undefined,
    bizType: type,
    reason: '',
    returnDate: '',
    transfereeId: undefined,
  });
  assets.value = [];
  assetSearchKeyword.value = '';
  dialogVisible.value = true;
  await Promise.all([
    searchAssets(''),
    type === 'transfer' && users.value.length === 0
      ? searchUsers('')
      : Promise.resolve(),
  ]);
}

async function searchAssets(keyword = '') {
  const generation = assetOptionsRequestGuard.next();
  assetSearchKeyword.value = keyword.trim();
  assetOptionsLoading.value = true;
  try {
    const currentUserId = Number(userStore.userInfo?.userId || 0);
    const result = await getAssetListApi(
      buildApprovalAssetQuery(form.bizType, currentUserId, keyword),
    );
    if (!assetOptionsRequestGuard.isLatest(generation)) return;
    assets.value = mergeApprovalAssetOptions(
      assets.value,
      result.items,
      form.assetId,
    );
  } catch {
    // 请求层已提示，保留当前选项。
  } finally {
    if (assetOptionsRequestGuard.isLatest(generation))
      assetOptionsLoading.value = false;
  }
}

async function searchUsers(keyword = '') {
  const requestGeneration = userOptionsRequestGuard.next();
  userOptionsLoading.value = true;
  try {
    const result = await getUserOptionsPageApi(keyword, 1, 50);
    if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
    users.value = mergeUserOptions(users.value, result.items);
  } catch {
    // 请求层已提示，保留已回填选项。
  } finally {
    if (userOptionsRequestGuard.isLatest(requestGeneration))
      userOptionsLoading.value = false;
  }
}

async function onBizTypeChange() {
  form.assetId = undefined;
  assets.value = [];
  assetSearchKeyword.value = '';
  await searchAssets('');
  if (form.bizType === 'transfer' && users.value.length === 0) {
    await searchUsers('');
  }
}

function onAssetChange() {
  form.returnDate = '';
}

async function submit() {
  if (!form.assetId) {
    ElMessage.warning('请选择资产');
    return;
  }
  if (showReturnDate.value && !form.returnDate) {
    ElMessage.warning('请选择归还日期');
    return;
  }
  if (showReturnDate.value && !isFutureReturnDate(form.returnDate)) {
    ElMessage.warning('归还日期必须晚于今天');
    return;
  }
  if (
    form.bizType === 'extension' &&
    (!selectedAsset.value?.returnDate ||
      !isValidExtensionReturnDate(
        form.returnDate,
        selectedAsset.value.returnDate,
      ))
  ) {
    ElMessage.warning('新归还日期必须晚于原应归还日期');
    return;
  }
  if (form.bizType === 'transfer' && !form.transfereeId) {
    ElMessage.warning('请选择接收人');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: form.assetId,
      bizType: form.bizType,
      reason: form.reason,
      returnDate: showReturnDate.value ? form.returnDate : undefined,
      transfereeId: form.bizType === 'transfer' ? form.transfereeId : undefined,
    });
    ElMessage.success('申请已提交');
    dialogVisible.value = false;
    activeSource.value = 'asset';
    assetPage.page = 1;
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    saving.value = false;
  }
}

async function withdraw(row: ApprovalWorkItem) {
  try {
    await ElMessageBox.confirm(
      `确认撤回申请「${row.flowNo}」？撤回后当前审批立即终止。`,
      '撤回申请',
      {
        cancelButtonText: '取消',
        confirmButtonText: '确认撤回',
        type: 'warning',
      },
    );
  } catch {
    return;
  }

  withdrawingKeys.value.push(row.key);
  try {
    await (row.source === 'asset'
      ? withdrawApprovalApi(row.id)
      : withdrawMaterialFlowApi(row.id));
    ElMessage.success('申请已撤回');
    await loadData();
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    withdrawingKeys.value = withdrawingKeys.value.filter(
      (key) => key !== row.key,
    );
  }
}

function statusText(status: string) {
  return (
    {
      approved: '已通过',
      pending: '审批中',
      rejected: '已驳回',
      withdrawn: '已撤回',
    }[status] ?? status
  );
}

function onPageSizeChange() {
  currentPage.value.page = 1;
  runHandled(loadData());
}

function search() {
  currentPage.value.page = 1;
  runHandled(loadData());
}

function resetQuery() {
  Object.assign(query, {
    bizType: '',
    keyword: '',
    status: '',
  });
  currentPage.value.page = 1;
  runHandled(loadData());
}

function onSourceChange() {
  query.bizType = '';
  currentPage.value.page = 1;
  runHandled(loadData());
}

function showProgress(row: ApprovalWorkItem) {
  selectedFlow.value = row;
  progressVisible.value = true;
}

function focusNotificationFlow() {
  const flowId = Number(route.query.flowId || 0);
  const source = notificationSource(route.query.source, route.path);
  if (!flowId) return false;
  const targetIndex = flows.value.findIndex(
    (item) => item.id === flowId && item.source === source,
  );
  if (targetIndex === -1) return false;
  showProgress(flows.value[targetIndex]!);
  return true;
}

watch(
  () => [route.query.flowId, route.query.source],
  () => {
    activeSource.value = notificationSource(route.query.source, route.path);
    currentPage.value.page = 1;
    runHandled(loadData());
  },
);

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
    <div class="mine-page">
      <div class="mine-header">
        <div>
          <h2 class="mine-title">我的申请</h2>
        </div>
        <div v-if="approvalActionAccess.canCreate" class="mine-actions">
          <ElButton type="success" @click="openStart('borrow')">
            发起借用
          </ElButton>
          <ElButton type="warning" @click="openStart('transfer')">
            发起转让
          </ElButton>
          <ElButton type="primary" @click="openStart('extension')">
            申请延期
          </ElButton>
          <ElButton @click="openStart('return')">发起归还</ElButton>
        </div>
      </div>

      <ElTabs v-model="activeSource" @tab-change="onSourceChange">
        <ElTabPane label="资产申请" name="asset" />
        <ElTabPane
          v-if="canViewMaterialFlow"
          label="料件申请"
          name="material"
        />
      </ElTabs>

      <div class="filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="关键字">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="流程编号/对象/借用人/接收人/事由"
              style="width: 280px"
              @keyup.enter="search"
            />
          </ElFormItem>
          <ElFormItem v-if="activeSource === 'asset'" label="业务类型">
            <ElSelect
              v-model="query.bizType"
              clearable
              placeholder="全部类型"
              style="width: 150px"
            >
              <ElOption label="借用" value="borrow" />
              <ElOption label="转让" value="transfer" />
              <ElOption label="延期" value="extension" />
              <ElOption label="归还" value="return" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="状态">
            <ElSelect
              v-model="query.status"
              clearable
              placeholder="全部状态"
              style="width: 130px"
            >
              <ElOption label="审批中" value="pending" />
              <ElOption label="已通过" value="approved" />
              <ElOption label="已驳回" value="rejected" />
              <ElOption label="已撤回" value="withdrawn" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="mine-table-panel">
        <ElTable :data="flows" border height="100%" v-loading="loading">
          <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
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
          <ElTableColumn align="center" label="类型" width="100">
            <template #default="{ row }">
              <ElTag
                :type="
                  row.bizType === 'borrow'
                    ? 'success'
                    : row.bizType === 'extension'
                      ? 'primary'
                      : row.bizType === 'transfer'
                        ? 'warning'
                        : row.bizType === 'material_transfer'
                          ? 'primary'
                          : 'info'
                "
                size="small"
              >
                {{ row.typeLabel }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="对象" min-width="180" prop="objectName" />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="借用人/接收人"
            min-width="140"
            prop="participant"
          />
          <ElTableColumn label="审批进度" min-width="310">
            <template #default="{ row }">
              <WorkflowProgressSummary
                :current-steps="row.raw.currentSteps"
                :next-steps="row.raw.nextSteps"
                :status="row.status"
              />
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" label="状态" width="100">
            <template #default="{ row }">
              <ElTag
                :type="
                  row.status === 'approved'
                    ? 'success'
                    : row.status === 'rejected'
                      ? 'danger'
                      : row.status === 'withdrawn'
                        ? 'info'
                        : 'warning'
                "
                size="small"
              >
                {{ statusText(row.status) }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            class-name="hide-on-mobile"
            label="申请事由"
            min-width="220"
            prop="reason"
          />
          <ElTableColumn align="center" fixed="right" label="操作" width="130">
            <template #default="{ row }">
              <ElButton link type="primary" @click="showProgress(row)">
                流程
              </ElButton>
              <ElButton
                v-if="canWithdrawApproval(row)"
                :loading="withdrawingKeys.includes(row.key)"
                link
                type="danger"
                @click="withdraw(row)"
              >
                撤回
              </ElButton>
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

      <ElDialog v-model="progressVisible" title="申请流转进度" width="620px">
        <WorkflowProgressDetail
          v-if="selectedFlow"
          :steps="selectedFlow.raw.progressSteps || []"
        />
      </ElDialog>

      <ElDialog v-model="dialogVisible" title="发起申请" width="540px">
        <ElForm class="start-approval-form" label-width="100px">
          <ElFormItem label="申请类型" required>
            <ElSelect
              v-model="form.bizType"
              style="width: 100%"
              @change="onBizTypeChange"
            >
              <ElOption label="借用" value="borrow" />
              <ElOption label="转让" value="transfer" />
              <ElOption label="延期" value="extension" />
              <ElOption label="归还" value="return" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="资产" required>
            <ElSelect
              v-model="form.assetId"
              :filter-method="searchAssets"
              :loading="assetOptionsLoading"
              :placeholder="assetSelectCopy.placeholder"
              clearable
              default-first-option
              filterable
              style="width: 100%"
              @change="onAssetChange"
            >
              <ElOption
                v-for="asset in assets"
                :key="asset.id"
                :label="`${asset.assetNo} / ${asset.name}`"
                :value="asset.id"
              />
              <template #empty>
                <div class="asset-select-empty">
                  {{ assetSelectCopy.emptyText }}
                </div>
              </template>
            </ElSelect>
            <div class="asset-select-help">
              {{ assetSelectCopy.helpText }}
            </div>
          </ElFormItem>
          <ElFormItem
            v-if="form.bizType === 'extension' && form.assetId"
            label="原应归还日期"
          >
            <ElInput :model-value="inheritedReturnDate" disabled />
            <div class="asset-select-help">
              延期审批通过前仍按原日期计算到期和逾期状态
            </div>
          </ElFormItem>
          <ElFormItem
            v-if="showReturnDate"
            :label="form.bizType === 'extension' ? '新归还日期' : '归还日期'"
            required
          >
            <ElDatePicker
              v-model="form.returnDate"
              :disabled-date="disableReturnDate"
              :placeholder="
                form.bizType === 'extension'
                  ? '选择新的归还日期'
                  : '选择归还日期'
              "
              style="width: 100%"
              type="date"
              value-format="YYYY-MM-DD"
            />
          </ElFormItem>
          <ElFormItem
            v-if="form.bizType === 'transfer'"
            label="接收人"
            required
          >
            <ElSelect
              v-model="form.transfereeId"
              :loading="userOptionsLoading"
              :remote-method="searchUsers"
              filterable
              placeholder="选择接收人"
              remote
              style="width: 100%"
            >
              <ElOption
                v-for="user in users"
                :key="user.id"
                :label="`${user.name}（${user.employeeNo}）`"
                :value="user.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem
            v-if="form.bizType === 'transfer' && form.assetId"
            label="原应归还日期"
          >
            <ElInput :model-value="inheritedReturnDate" disabled />
            <div class="asset-select-help">
              转让只变更当前保管人，不会重新计算原借用期限
            </div>
          </ElFormItem>
          <ElFormItem label="申请事由">
            <ElInput
              v-model="form.reason"
              :rows="3"
              placeholder="请输入申请事由"
              type="textarea"
            />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="submit">
            提交
          </ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
/* ========== 设计系统规范 ========== */
.mine-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}

/* ========== 页面头部 ========== */
.mine-header {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border: 1px solid var(--asset-page-border-strong);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.mine-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: var(--asset-page-text);
  letter-spacing: 0;
}

.mine-actions {
  display: flex;
  gap: 12px;
}

/* ========== 表格面板 ========== */
.mine-table-panel {
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

.mine-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
  font-size: 14px;
  line-height: 20px;
}

.mine-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-panel-header-solid);
  color: var(--asset-page-panel-header-text);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.mine-table-panel :deep(.el-table--border) {
  border: none;
}

.mine-table-panel :deep(.el-table td.el-table__cell),
.mine-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.mine-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

.mine-empty-text {
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
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

:deep(.el-form-item) {
  margin-bottom: 20px;
}

:deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

.start-approval-form :deep(.el-form-item__label) {
  align-items: center;
  line-height: var(--el-component-size);
}

.asset-select-help {
  width: 100%;
  margin-top: 6px;
  font-size: 12px;
  line-height: 18px;
  color: var(--asset-page-muted);
}

.asset-select-empty {
  padding: 8px 16px;
  font-size: 13px;
  line-height: 20px;
  color: var(--asset-page-muted);
  text-align: center;
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
