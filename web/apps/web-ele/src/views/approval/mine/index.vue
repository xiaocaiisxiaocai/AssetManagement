<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';
import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';
import { useAccess } from '@vben/access';

import { getAllAssetsApi } from '#/api/asset';
import { getUserOptionsApi } from '#/api/user';
import {
  disableNonFutureReturnDate,
  isFutureReturnDate,
} from '#/utils/return-date';
import { listMyFlowsApi, withdrawMaterialFlowApi } from '#/api/material';
import {
  getMineApprovalsApi,
  startApprovalApi,
  withdrawApprovalApi,
} from '#/api/workflow';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildApprovalActionAccess } from '#/views/permissions/action-access';
import {
  canWithdrawApproval,
  mergeApprovalWorkItems,
} from '../approval-work-items';

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
  ElTag,
} from 'element-plus';

defineOptions({ name: 'ApprovalMine' });

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
const flows = ref<ApprovalWorkItem[]>([]);
const assets = ref<AssetItem[]>([]);
const users = ref<UserOptionDto[]>([]);
const pageSizeOptions = ref(createPageSizeOptions(20));
const form = reactive({
  assetId: undefined as number | undefined,
  bizType: 'borrow',
  reason: '',
  returnDate: '',
  transfereeId: undefined as number | undefined,
});
const query = reactive({
  page: 1,
  pageSize: 20,
});
const showReturnDate = computed(() => form.bizType === 'borrow');
const pagedFlows = computed(() => {
  const start = (query.page - 1) * query.pageSize;
  return flows.value.slice(start, start + query.pageSize);
});

async function loadData() {
  loading.value = true;
  try {
    const materialMinePromise = canViewMaterialFlow.value
      ? listMyFlowsApi()
      : Promise.resolve([]);
    const [mine, materialMine] = await Promise.all([
      getMineApprovalsApi(),
      materialMinePromise,
    ]);
    flows.value = mergeApprovalWorkItems(mine, materialMine);
    if ((query.page - 1) * query.pageSize >= flows.value.length) {
      query.page = 1;
    }
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    loading.value = false;
  }
}

async function openStart(type = 'borrow') {
  Object.assign(form, {
    assetId: undefined,
    bizType: type,
    reason: '',
    returnDate: '',
    transfereeId: undefined,
  });
  if (assets.value.length === 0) {
    try {
      assets.value = await getAllAssetsApi();
    } catch {
      return;
    }
  }
  if (users.value.length === 0) {
    try {
      users.value = await getUserOptionsApi();
    } catch {
      return;
    }
  }
  dialogVisible.value = true;
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
    if (row.source === 'asset') {
      await withdrawApprovalApi(row.id);
    } else {
      await withdrawMaterialFlowApi(row.id);
    }
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
  query.page = 1;
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
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
          <ElButton type="success" @click="openStart('borrow')"
            >发起借用</ElButton
          >
          <ElButton type="warning" @click="openStart('transfer')"
            >发起转让</ElButton
          >
          <ElButton @click="openStart('return')">发起归还</ElButton>
        </div>
      </div>

      <div class="mine-table-panel">
        <ElTable v-loading="loading" :data="pagedFlows" border height="100%">
          <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
          <ElTableColumn label="来源" width="110" align="center">
            <template #default="{ row }">
              <ElTag
                :type="row.source === 'asset' ? 'primary' : 'success'"
                size="small"
              >
                {{ row.sourceLabel }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="类型" width="100" align="center">
            <template #default="{ row }">
              <ElTag
                :type="
                  row.bizType === 'borrow'
                    ? 'success'
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
          <ElTableColumn label="当前节点" min-width="150">
            <template #default="{ row }">
              <span
                :class="{ 'mine-empty-text': row.currentNodeLabel === '-' }"
              >
                {{ row.currentNodeLabel }}
              </span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="100" align="center">
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
          <ElTableColumn fixed="right" label="操作" width="100" align="center">
            <template #default="{ row }">
              <ElButton
                v-if="canWithdrawApproval(row)"
                :loading="withdrawingKeys.includes(row.key)"
                link
                type="danger"
                @click="withdraw(row)"
              >
                撤回
              </ElButton>
              <span v-else class="mine-empty-text">-</span>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ flows.length }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="query.pageSize"
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
            v-model:current-page="query.page"
            :page-size="query.pageSize"
            :total="flows.length"
            background
            layout="prev, pager, next"
          />
        </div>
      </div>

      <ElDialog v-model="dialogVisible" title="发起申请" width="540px">
        <ElForm class="start-approval-form" label-width="100px">
          <ElFormItem label="申请类型" required>
            <ElSelect v-model="form.bizType" style="width: 100%">
              <ElOption label="借用" value="borrow" />
              <ElOption label="转让" value="transfer" />
              <ElOption label="归还" value="return" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="资产" required>
            <ElSelect
              v-model="form.assetId"
              filterable
              placeholder="选择资产"
              style="width: 100%"
            >
              <ElOption
                v-for="asset in assets"
                :key="asset.id"
                :label="`${asset.assetNo} / ${asset.name}`"
                :value="asset.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem v-if="showReturnDate" label="归还日期" required>
            <ElDatePicker
              v-model="form.returnDate"
              :disabled-date="disableNonFutureReturnDate"
              type="date"
              value-format="YYYY-MM-DD"
              placeholder="选择归还日期"
              style="width: 100%"
            />
          </ElFormItem>
          <ElFormItem
            v-if="form.bizType === 'transfer'"
            label="接收人"
            required
          >
            <ElSelect
              v-model="form.transfereeId"
              filterable
              placeholder="选择接收人"
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
          <ElFormItem label="申请事由">
            <ElInput
              v-model="form.reason"
              :rows="3"
              type="textarea"
              placeholder="请输入申请事由"
            />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="submit"
            >提交</ElButton
          >
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

:deep(.el-input__inner) {
  font-size: 14px;
  line-height: 20px;
}

:deep(.el-textarea__inner) {
  font-size: 14px;
  line-height: 20px;
}
</style>
