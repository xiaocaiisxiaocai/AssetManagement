<script lang="ts" setup>
import type { AssetImportPreviewRow } from '#/api/asset';

import { computed, ref, watch } from 'vue';

import {
  ElAlert,
  ElButton,
  ElDialog,
  ElMessage,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  confirmAssetImportApi,
  downloadAssetImportTemplateApi,
  validateAssetImportApi,
} from '#/api/asset';
import { formatDate } from '#/utils/date-format';
import { downloadBlob } from '#/utils/download';
import { createImportValidationSession } from '#/utils/import-validation-session';

import {
  canConfirmAssetImport,
  summarizeAssetImportRows,
} from '../asset-import-rules';

const emit = defineEmits<{ imported: [] }>();
const visible = defineModel<boolean>('visible', { required: true });

const fileInput = ref<HTMLInputElement | null>(null);
const importValidation = createImportValidationSession<AssetImportPreviewRow>();
const importing = importValidation.loading;
const rows = importValidation.rows;
const selectedFile = importValidation.selectedFile;
const summary = computed(() => summarizeAssetImportRows(rows.value));
const canConfirm = computed(() =>
  canConfirmAssetImport(importing.value, !!selectedFile.value, rows.value),
);

watch(visible, () => {
  importValidation.reset();
  if (fileInput.value) fileInput.value.value = '';
});

async function downloadTemplate() {
  try {
    const response = await downloadAssetImportTemplateApi();
    downloadBlob(response.data, '资产批量导入模板.xlsx');
  } catch {
    // 错误由请求拦截器统一提示。
  }
}

function chooseFile() {
  if (!fileInput.value) return;
  fileInput.value.value = '';
  fileInput.value.click();
}

async function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const requestGeneration = importValidation.start(input.files?.[0] ?? null);
  if (requestGeneration === null || !selectedFile.value) return;

  const file = selectedFile.value;
  try {
    const result = await validateAssetImportApi(file);
    if (!importValidation.canApply(requestGeneration)) return;
    rows.value = result;
    const resultSummary = summarizeAssetImportRows(result);
    if (resultSummary.totalCount === 0) {
      ElMessage.warning('导入文件中没有资产数据');
    } else if (resultSummary.invalidCount > 0) {
      ElMessage.warning(
        `发现 ${resultSummary.invalidCount} 条不导入记录，其余 ${resultSummary.validCount} 条可继续导入`,
      );
    } else {
      ElMessage.success(
        `分类与数据校验通过，共 ${resultSummary.validCount} 条`,
      );
    }
  } finally {
    importValidation.finish(requestGeneration);
  }
}

async function confirmImport() {
  if (!selectedFile.value || !canConfirm.value) {
    ElMessage.warning('请先选择文件，并确保至少有一条可导入记录');
    return;
  }

  importing.value = true;
  try {
    const result = await confirmAssetImportApi(selectedFile.value);
    rows.value = result.rows;
    if (result.successCount === 0) {
      ElMessage.warning('没有可导入记录，请检查分类编码后重新选择文件');
      return;
    }
    const skippedText =
      result.failedCount > 0 ? `，未导入 ${result.failedCount} 条` : '';
    ElMessage.success(`成功导入 ${result.successCount} 条资产${skippedText}`);
    visible.value = false;
    emit('imported');
  } finally {
    importing.value = false;
  }
}
</script>

<template>
  <ElDialog
    v-model="visible"
    destroy-on-close
    title="批量导入资产"
    width="1040px"
  >
    <ElAlert
      :closable="false"
      class="asset-import-alert"
      show-icon
      title="模板第二行为填写范例，请替换或删除。系统会先检查分类、部门、保管人和资产编号；保管人可填姓名或工号，同名时请填工号；资产编号留空则自动生成。"
      type="info"
    />

    <div class="asset-import-toolbar">
      <ElButton @click="downloadTemplate">下载模板</ElButton>
      <input
        ref="fileInput"
        accept=".xlsx"
        class="asset-import-file-input"
        type="file"
        @change="onFileChange"
      />
      <ElButton :loading="importing" @click="chooseFile">选择文件</ElButton>
      <span class="asset-import-file-name">
        {{ selectedFile?.name || '未选择文件' }}
      </span>
      <ElButton
        :disabled="!canConfirm"
        :loading="importing"
        type="primary"
        @click="confirmImport"
      >
        导入有效记录
      </ElButton>
    </div>

    <div class="asset-import-summary">
      <div>
        <span>文件记录</span>
        <strong>{{ summary.totalCount }}</strong>
      </div>
      <div class="is-valid">
        <span>可导入</span>
        <strong>{{ summary.validCount }}</strong>
      </div>
      <div class="is-invalid">
        <span>不导入</span>
        <strong>{{ summary.invalidCount }}</strong>
      </div>
      <div v-if="summary.missingCategoryCount > 0" class="missing-category">
        其中 {{ summary.missingCategoryCount }} 条分类不存在
      </div>
    </div>

    <ElTable :data="rows" border max-height="420">
      <ElTableColumn label="行号" prop="row" width="70" />
      <ElTableColumn label="资产编号" min-width="150">
        <template #default="{ row }">
          {{ row.assetNo || '自动生成' }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="资产名称" min-width="150" prop="name" />
      <ElTableColumn label="分类编码" min-width="150" prop="categoryCode" />
      <ElTableColumn label="归属部门" min-width="130" prop="departmentName" />
      <ElTableColumn label="保管人" min-width="150">
        <template #default="{ row }">
          <span v-if="row.custodianName">
            {{ row.custodianName }}（{{ row.custodianEmployeeNo }}）
          </span>
          <span v-else>{{ row.custodianEmployeeNo || '-' }}</span>
        </template>
      </ElTableColumn>
      <ElTableColumn label="存放位置" min-width="150" prop="locationName" />
      <ElTableColumn label="数量" prop="quantity" width="75" />
      <ElTableColumn label="购入日期" width="115">
        <template #default="{ row }">
          {{ formatDate(row.purchaseDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="登记日期" width="115">
        <template #default="{ row }">
          {{ formatDate(row.registrationTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="状态" width="96">
        <template #default="{ row }">
          <ElTag :type="row.isValid ? 'success' : 'danger'" size="small">
            {{ row.isValid ? '可导入' : '不导入' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="校验结果" min-width="260">
        <template #default="{ row }">
          <span :class="row.isValid ? 'valid-result' : 'invalid-result'">
            {{ row.isValid ? '分类及数据有效' : row.error }}
          </span>
        </template>
      </ElTableColumn>
    </ElTable>
  </ElDialog>
</template>

<style scoped>
.asset-import-alert {
  margin-bottom: 14px;
}

.asset-import-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}

.asset-import-file-input {
  display: none;
}

.asset-import-file-name {
  min-width: 180px;
  max-width: 300px;
  overflow: hidden;
  color: var(--asset-page-text-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.asset-import-summary {
  display: flex;
  gap: 24px;
  align-items: center;
  min-height: 48px;
  padding: 8px 14px;
  margin-bottom: 12px;
  background: var(--asset-page-surface-soft);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
}

.asset-import-summary > div:not(.missing-category) {
  display: flex;
  gap: 8px;
  align-items: baseline;
  color: var(--asset-page-text-secondary);
}

.asset-import-summary strong {
  font-size: 18px;
  color: var(--asset-page-text);
}

.asset-import-summary .is-valid strong,
.valid-result {
  color: var(--el-color-success);
}

.asset-import-summary .is-invalid strong,
.invalid-result,
.missing-category {
  color: var(--el-color-danger);
}

.missing-category {
  margin-left: auto;
  font-size: 13px;
}
</style>
