<script lang="ts" setup>
import type { AuditCleanupPreview, AuditLogQuery, AuditLogRow } from '#/api/report';

import { onMounted, reactive, ref } from 'vue';

import {
  cleanupAuditLogsApi,
  exportAuditLogsApi,
  getAuditCleanupPreviewApi,
  getAuditLogsApi,
} from '#/api/report';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';

import {
  ElButton,
  ElDatePicker,
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
  ElTag,
  ElTooltip,
} from 'element-plus';

defineOptions({ name: 'AdminAudit' });

type TagType = 'danger' | 'info' | 'success' | 'warning';

const loading = ref(false);
const cleanupLoading = ref(false);
const cleanupPreviewLoading = ref(false);
const cleanupDialogVisible = ref(false);
const rows = ref<AuditLogRow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));
const cleanupRetentionDays = ref(30);
const cleanupPreview = ref<AuditCleanupPreview | null>(null);
const query = reactive({
  actionType: '',
  dateRange: [] as string[],
  module: '',
  page: 1,
  pageSize: 20,
  userId: undefined as number | undefined,
});

async function loadData() {
  loading.value = true;
  try {
    const result = await getAuditLogsApi(buildQuery());
    rows.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function buildQuery(): AuditLogQuery {
  return {
    actionType: query.actionType || undefined,
    endTime: query.dateRange[1],
    module: query.module || undefined,
    page: query.page,
    pageSize: query.pageSize,
    startTime: query.dateRange[0],
    userId: query.userId,
  };
}

function search() {
  query.page = 1;
  void loadData();
}

function resetQuery() {
  Object.assign(query, {
    actionType: '',
    dateRange: [],
    module: '',
    page: 1,
    userId: undefined,
  });
  void loadData();
}

async function exportReport() {
  try {
    const response = await exportAuditLogsApi(buildQuery());
    downloadBlob(response.data, 'audit-logs.xlsx');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  }
}

async function openCleanupDialog() {
  cleanupDialogVisible.value = true;
  await loadCleanupPreview();
}

async function loadCleanupPreview() {
  cleanupPreviewLoading.value = true;
  try {
    cleanupPreview.value = await getAuditCleanupPreviewApi(cleanupRetentionDays.value);
  } finally {
    cleanupPreviewLoading.value = false;
  }
}

async function confirmCleanup() {
  const preview = cleanupPreview.value ?? await getAuditCleanupPreviewApi(cleanupRetentionDays.value);
  await ElMessageBox.confirm(
    `确认清理 ${formatTime(preview.cutoffTime)} 之前的审计日志？预计删除 ${preview.deleteCount} 条。`,
    '清理审计日志',
    {
      confirmButtonText: '确认清理',
      cancelButtonText: '取消',
      type: 'warning',
    },
  );

  cleanupLoading.value = true;
  try {
    const result = await cleanupAuditLogsApi(cleanupRetentionDays.value);
    ElMessage.success(`已清理 ${result.deletedCount} 条审计日志`);
    cleanupDialogVisible.value = false;
    await loadData();
  } finally {
    cleanupLoading.value = false;
  }
}

function actionTypeLabel(type: string): string {
  const map: Record<string, string> = { POST: '新增', PUT: '修改', DELETE: '删除', business: '业务', cleanup: '清理', remind: '催办' };
  return map[type] ?? type;
}

function actionType(type: string): TagType | undefined {
  if (type === 'remind') return 'warning';
  if (type === 'business') return 'info';
  if (type === 'cleanup') return 'info';
  if (type === 'DELETE') return 'danger';
  if (type === 'POST') return 'success';
  if (type === 'PUT') return 'info';
  return undefined;
}

const moduleLabelMap: Record<string, string> = {
  Approval: '审批流转',
  Asset: '资产',
  AssetCategory: '资产分类',
  AuditLog: '审计日志',
  Auth: '登录认证',
  DatabaseBackup: '数据库备份',
  Department: '部门',
  File: '文件',
  Location: '位置',
  MaterialFlow: '料件流转',
  Menu: '菜单',
  Notification: '通知',
  Permission: '权限',
  RbacMenu: '菜单',
  Role: '角色',
  Setting: '系统参数',
  TestMaterial: '测试料件',
  TestProject: '测试项目',
  User: '用户',
  Workflow: '工作流',
};

function moduleLabel(type?: null | string): string {
  if (!type) return '-';
  return moduleLabelMap[type] ?? type;
}

interface AuditChange {
  after?: unknown;
  before?: unknown;
  field: string;
}

interface ParsedDetail {
  changes: AuditChange[];
  error?: null | string;
  statusCode?: null | number;
  success: boolean;
}

const emptyDetail: ParsedDetail = { changes: [], success: true };

function parseDetail(detail?: null | string): ParsedDetail {
  if (!detail) return emptyDetail;
  try {
    const parsed = JSON.parse(detail) as {
      changes?: AuditChange[];
      error?: null | string;
      statusCode?: null | number;
      success?: boolean;
    };
    return {
      changes: Array.isArray(parsed.changes) ? parsed.changes : [],
      error: parsed.error,
      statusCode: parsed.statusCode,
      success: parsed.success ?? true,
    };
  } catch {
    return emptyDetail;
  }
}

function formatDuration(ms?: null | number): string {
  if (ms === null || ms === undefined) return '-';
  if (ms < 1000) return `${ms} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '(空)';
  if (typeof value === 'boolean') return value ? '是' : '否';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}


function formatTime(value: string) {
  return value?.replace('T', ' ').slice(0, 19);
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
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
          <h2 class="page-title">审计日志</h2>
          <p class="page-subtitle">系统操作记录查询与追踪</p>
        </div>
        <div class="header-actions">
          <ElButton type="warning" @click="openCleanupDialog">清理日志</ElButton>
          <ElButton type="primary" @click="exportReport">导出</ElButton>
        </div>
      </div>

      <div class="filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="时间">
            <ElDatePicker
              v-model="query.dateRange"
              end-placeholder="结束日期"
              format="YYYY-MM-DD"
              range-separator="至"
              start-placeholder="开始日期"
              type="daterange"
              value-format="YYYY-MM-DD"
              style="width: 240px"
            />
          </ElFormItem>
          <ElFormItem label="操作">
            <ElSelect
              v-model="query.actionType"
              aria-label="操作类型"
              clearable
              placeholder="全部操作"
              style="width: 130px"
            >
              <ElOption label="新增" value="POST" />
              <ElOption label="修改" value="PUT" />
              <ElOption label="删除" value="DELETE" />
              <ElOption label="催办" value="remind" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="模块">
            <ElInput v-model="query.module" clearable placeholder="模块/摘要关键字" style="width: 180px" />
          </ElFormItem>
          <ElFormItem label="用户ID">
            <ElInput v-model.number="query.userId" clearable placeholder="用户ID" style="width: 120px" />
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="table-panel-with-toolbar">
        <ElTable v-loading="loading" :data="rows" border height="100%">
          <ElTableColumn type="expand">
            <template #default="{ row }">
              <div class="audit-detail">
                <ElDescriptions :column="3" border size="small">
                  <ElDescriptionsItem label="结果">
                    <ElTag
                      :type="parseDetail(row.detail).success ? 'success' : 'danger'"
                      size="small"
                    >
                      {{ parseDetail(row.detail).success ? '成功' : '失败' }}
                    </ElTag>
                  </ElDescriptionsItem>
                  <ElDescriptionsItem label="状态码">
                    {{ parseDetail(row.detail).statusCode ?? '-' }}
                  </ElDescriptionsItem>
                  <ElDescriptionsItem label="耗时">
                    {{ formatDuration(row.durationMs) }}
                  </ElDescriptionsItem>
                  <ElDescriptionsItem :span="2" label="请求">
                    {{ row.summary }}
                  </ElDescriptionsItem>
                  <ElDescriptionsItem label="IP">{{ row.ip || '-' }}</ElDescriptionsItem>
                  <ElDescriptionsItem :span="3" label="客户端">
                    {{ row.userAgent || '-' }}
                  </ElDescriptionsItem>
                </ElDescriptions>

                <div v-if="parseDetail(row.detail).error" class="audit-error">
                  错误信息:{{ parseDetail(row.detail).error }}
                </div>

                <div class="audit-changes">
                  <div class="audit-changes-title">字段变更</div>
                  <ElTable
                    v-if="parseDetail(row.detail).changes.length"
                    :data="parseDetail(row.detail).changes"
                    border
                    size="small"
                  >
                    <ElTableColumn label="字段" min-width="160" prop="field" />
                    <ElTableColumn label="原值" min-width="220">
                      <template #default="{ row: change }">
                        <span class="audit-value-before">{{ formatValue(change.before) }}</span>
                      </template>
                    </ElTableColumn>
                    <ElTableColumn label="新值" min-width="220">
                      <template #default="{ row: change }">
                        <span class="audit-value-after">{{ formatValue(change.after) }}</span>
                      </template>
                    </ElTableColumn>
                  </ElTable>
                  <div v-else class="audit-changes-empty">
                    无字段级变更(该操作未涉及可追踪实体或属性未变化)
                  </div>
                </div>
              </div>
            </template>
          </ElTableColumn>
          <ElTableColumn label="时间" min-width="170">
            <template #default="{ row }">{{ formatTime(row.occurredAt) }}</template>
          </ElTableColumn>
          <ElTableColumn label="操作人" min-width="120">
            <template #default="{ row }">{{ row.userName || row.userId || '-' }}</template>
          </ElTableColumn>
          <ElTableColumn label="操作" width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="actionType(row.actionType)" size="small">{{ actionTypeLabel(row.actionType) }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="模块" min-width="120">
            <template #default="{ row }">
              <ElTooltip
                v-if="row.targetType"
                :content="row.targetType"
                placement="top"
              >
                <span>{{ moduleLabel(row.targetType) }}</span>
              </ElTooltip>
              <span v-else>-</span>
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="目标ID" min-width="90" prop="targetId" />
          <ElTableColumn label="摘要" min-width="240" prop="summary" show-overflow-tooltip />
          <ElTableColumn class-name="hide-on-mobile" label="IP" min-width="130" prop="ip" />
          <ElTableColumn class-name="hide-on-mobile" label="耗时" width="96" align="right">
            <template #default="{ row }">{{ formatDuration(row.durationMs) }}</template>
          </ElTableColumn>
        </ElTable>

        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ total }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="query.pageSize"
              aria-label="审计日志每页条数"
              style="width: 92px"
              @change="search"
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
            :total="total"
            background
            layout="prev, pager, next"
            @current-change="loadData"
          />
        </div>
      </div>

      <ElDialog
        v-model="cleanupDialogVisible"
        title="清理审计日志"
        width="460px"
        :close-on-click-modal="false"
      >
        <ElForm label-position="top">
          <ElFormItem label="保留周期">
            <ElRadioGroup v-model="cleanupRetentionDays" @change="loadCleanupPreview">
              <ElRadioButton :value="7">7 天</ElRadioButton>
              <ElRadioButton :value="14">14 天</ElRadioButton>
              <ElRadioButton :value="30">30 天</ElRadioButton>
            </ElRadioGroup>
          </ElFormItem>

          <div v-loading="cleanupPreviewLoading" class="cleanup-preview">
            <div class="preview-row">
              <span>截止时间</span>
              <strong>{{ cleanupPreview ? formatTime(cleanupPreview.cutoffTime) : '-' }}</strong>
            </div>
            <div class="preview-row">
              <span>预计删除</span>
              <strong>{{ cleanupPreview?.deleteCount ?? 0 }} 条</strong>
            </div>
            <div class="preview-tip">
              只会删除超过保留周期的审计日志；本次清理动作会单独写入审计日志。
            </div>
          </div>
        </ElForm>

        <template #footer>
          <ElButton @click="cleanupDialogVisible = false">取消</ElButton>
          <ElButton
            type="danger"
            :loading="cleanupLoading"
            :disabled="cleanupPreviewLoading"
            @click="confirmCleanup"
          >
            确认清理
          </ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
.header-actions {
  display: flex;
  gap: 8px;
}

.audit-detail {
  padding: 8px 16px 12px;
}

.audit-error {
  padding: 8px 12px;
  margin-top: 10px;
  font-size: 13px;
  color: var(--el-color-danger);
  background: var(--el-color-danger-light-9);
  border: 1px solid var(--el-color-danger-light-7);
  border-radius: 4px;
}

.audit-changes {
  margin-top: 12px;
}

.audit-changes-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.audit-changes-empty {
  padding: 10px 12px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
}

.audit-value-before {
  color: var(--el-text-color-secondary);
  text-decoration: line-through;
}

.audit-value-after {
  color: var(--el-color-success);
}

.cleanup-preview {
  min-height: 116px;
  padding: 12px;
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.preview-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
  color: var(--el-text-color-regular);
}

.preview-row strong {
  color: var(--el-text-color-primary);
}

.preview-tip {
  padding-top: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
  border-top: 1px dashed var(--el-border-color);
}
</style>
