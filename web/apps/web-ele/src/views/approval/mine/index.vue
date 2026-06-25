<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';
import type { ApprovalFlow } from '#/api/workflow';

import { computed, onMounted, reactive, ref } from 'vue';

import { getAssetListApi } from '#/api/asset';
import { getMineApprovalsApi, startApprovalApi } from '#/api/workflow';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'ApprovalMine' });

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const flows = ref<ApprovalFlow[]>([]);
const assets = ref<AssetItem[]>([]);
const form = reactive({
  assetId: undefined as number | undefined,
  bizType: 'borrow',
  reason: '',
  returnDate: '',
});
const showReturnDate = computed(() => form.bizType === 'borrow');

async function loadData() {
  loading.value = true;
  try {
    const [mine, assetPage] = await Promise.all([
      getMineApprovalsApi(),
      getAssetListApi({ page: 1, pageSize: 200 }),
    ]);
    flows.value = mine;
    assets.value = assetPage.items;
  } catch (error: any) {
    ElMessage.error(error.message || '加载我的申请失败');
  } finally {
    loading.value = false;
  }
}

function openStart(type = 'borrow') {
  Object.assign(form, {
    assetId: undefined,
    bizType: type,
    reason: '',
    returnDate: '',
  });
  dialogVisible.value = true;
}

async function submit() {
  if (!form.assetId) {
    ElMessage.warning('请选择资产');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: form.assetId,
      bizType: form.bizType,
      reason: form.reason,
      returnDate: showReturnDate.value ? form.returnDate : undefined,
    });
    ElMessage.success('申请已提交');
    dialogVisible.value = false;
    await loadData();
  } catch (error: any) {
    ElMessage.error(error.message || '提交申请失败');
  } finally {
    saving.value = false;
  }
}

function bizText(type: string) {
  return { borrow: '借用', return: '归还', transfer: '转让' }[type] ?? type;
}

function statusText(status: string) {
  return { approved: '已通过', pending: '审批中', rejected: '已驳回' }[status] ?? status;
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="mine-page">
      <div class="mine-header">
        <div>
          <h2 class="mine-title">我的申请</h2>
          <p class="mine-subtitle">我发起的审批申请记录</p>
        </div>
        <div class="mine-actions">
          <ElButton type="success" @click="openStart('borrow')">发起借用</ElButton>
          <ElButton type="warning" @click="openStart('transfer')">发起转让</ElButton>
          <ElButton @click="openStart('return')">发起归还</ElButton>
        </div>
      </div>

      <div class="mine-table-panel">
        <ElTable v-loading="loading" :data="flows" border>
          <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
          <ElTableColumn label="类型" width="100" align="center">
            <template #default="{ row }">
              <ElTag
                :type="row.bizType === 'borrow' ? 'success' : row.bizType === 'transfer' ? 'warning' : 'info'"
                size="small"
              >
                {{ bizText(row.bizType) }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="资产" min-width="180" prop="assetName" />
          <ElTableColumn label="当前节点" min-width="150">
            <template #default="{ row }">
              <span v-if="row.currentNodeIds?.length === 1">
                {{ row.bpmnTokens?.[row.currentNodeIds[0]]?.nodeName || '-' }}
              </span>
              <ElTag v-else-if="row.currentNodeIds?.length > 1" type="info" size="small">
                {{ row.currentNodeIds.length }} 个并行节点
              </ElTag>
              <span v-else class="mine-empty-text">-</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="100" align="center">
            <template #default="{ row }">
              <ElTag
                :type="row.status === 'approved' ? 'success' : row.status === 'rejected' ? 'danger' : 'warning'"
                size="small"
              >
                {{ statusText(row.status) }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="申请事由" min-width="220" prop="reason" />
        </ElTable>
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
            <ElSelect v-model="form.assetId" filterable placeholder="选择资产" style="width: 100%">
              <ElOption
                v-for="asset in assets"
                :key="asset.id"
                :label="`${asset.assetNo} / ${asset.name}`"
                :value="asset.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem v-if="showReturnDate" label="归还日期" required>
            <ElInput v-model="form.returnDate" placeholder="YYYY-MM-DD，借用时填写" />
          </ElFormItem>
          <ElFormItem label="申请事由">
            <ElInput v-model="form.reason" :rows="3" type="textarea" placeholder="请输入申请事由" />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="submit">提交</ElButton>
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
  min-height: calc(100vh - 112px);
}

/* ========== 页面头部 ========== */
.mine-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
  box-shadow: var(--asset-page-shadow);
}

.mine-title {
  margin: 0 0 4px 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: var(--asset-page-text);
  letter-spacing: -0.02em;
}

.mine-subtitle {
  margin: 0;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.mine-actions {
  display: flex;
  gap: 12px;
}

/* ========== 表格面板 ========== */
.mine-table-panel {
  flex: 1;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  overflow: hidden;
}

.mine-table-panel :deep(.el-table) {
  font-size: 14px;
  line-height: 20px;
}

.mine-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
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
