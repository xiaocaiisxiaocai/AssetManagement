<script lang="ts" setup>
import type { TestProjectImportRow } from '#/api/test-project';

import { computed, ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElMessage,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  confirmTestProjectImportApi,
  downloadTestProjectImportTemplateApi,
  validateTestProjectImportApi,
} from '#/api/test-project';
import { formatDate } from '#/utils/date-format';
import { downloadBlob } from '#/utils/download';
import { createImportValidationSession } from '#/utils/import-validation-session';

const emit = defineEmits<{ imported: [] }>();
const visible = defineModel<boolean>('visible', { required: true });

const fileInput = ref<HTMLInputElement | null>(null);
const importValidation = createImportValidationSession<TestProjectImportRow>();
const importing = importValidation.loading;
const rows = importValidation.rows;
const selectedFile = importValidation.selectedFile;
const hasInvalidRows = computed(() => rows.value.some((row) => !row.isValid));
const canConfirm = computed(
  () =>
    !importing.value &&
    !!selectedFile.value &&
    rows.value.length > 0 &&
    !hasInvalidRows.value,
);

watch(visible, (opened) => {
  importValidation.reset();
  if (!opened) return;
  if (fileInput.value) fileInput.value.value = '';
});

async function downloadTemplate() {
  try {
    const response = await downloadTestProjectImportTemplateApi();
    downloadBlob(response.data, '测试项目批量导入模板.xlsx');
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
    const result = await validateTestProjectImportApi(file);
    if (!importValidation.canApply(requestGeneration)) return;
    rows.value = result.rows;
    if (result.rows.length === 0) {
      ElMessage.warning('导入文件中没有项目数据');
    } else if (result.failedCount > 0) {
      ElMessage.warning(
        `发现 ${result.failedCount} 条错误，请修正后重新选择文件`,
      );
    } else {
      ElMessage.success(`校验通过，共 ${result.successCount} 条`);
    }
  } finally {
    importValidation.finish(requestGeneration);
  }
}

async function confirmImport() {
  if (!selectedFile.value) {
    ElMessage.warning('请先选择 Excel 文件');
    return;
  }
  importing.value = true;
  try {
    const result = await confirmTestProjectImportApi(selectedFile.value);
    rows.value = result.rows;
    if (result.failedCount > 0) {
      ElMessage.warning(`导入失败 ${result.failedCount} 条，请修正后重新导入`);
      return;
    }
    ElMessage.success(`成功导入 ${result.successCount} 个测试项目`);
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
    title="批量导入测试项目"
    width="1080px"
  >
    <div class="project-import-toolbar">
      <ElButton @click="downloadTemplate">下载模板</ElButton>
      <input
        ref="fileInput"
        accept=".xlsx"
        class="project-import-file-input"
        type="file"
        @change="onFileChange"
      />
      <ElButton :loading="importing" @click="chooseFile">选择文件</ElButton>
      <span class="project-import-file-name">
        {{ selectedFile?.name || '未选择文件' }}
      </span>
      <ElButton
        :disabled="!canConfirm"
        :loading="importing"
        type="primary"
        @click="confirmImport"
      >
        确认导入
      </ElButton>
    </div>

    <div class="project-import-hint">
      请使用模板填写，项目类型和项目进度均填写中文名称，负责人请填写唯一工号。
    </div>

    <ElTable :data="rows" border max-height="420">
      <ElTableColumn label="行号" prop="row" width="70" />
      <ElTableColumn label="项目编号" min-width="130" prop="code" />
      <ElTableColumn label="项目名称" min-width="150" prop="name" />
      <ElTableColumn label="项目类型" min-width="120">
        <template #default="{ row }">
          {{ row.projectTypeLabel || row.projectTypeCode || '-' }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="负责人" min-width="120">
        <template #default="{ row }">
          {{ row.ownerName || row.ownerEmployeeNo || '-' }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="开始时间" width="110">
        <template #default="{ row }">{{ formatDate(row.startDate) }}</template>
      </ElTableColumn>
      <ElTableColumn label="计划完成" width="110">
        <template #default="{ row }">
          {{ formatDate(row.plannedFinishDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="进度" min-width="110">
        <template #default="{ row }">
          {{ row.progressLabel || row.progressCode || '-' }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="状态" width="80">
        <template #default="{ row }">
          <ElTag :type="row.isValid ? 'success' : 'danger'" size="small">
            {{ row.isValid ? '有效' : '无效' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="错误" min-width="260" prop="error" />
    </ElTable>
  </ElDialog>
</template>

<style scoped>
.project-import-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}

.project-import-file-input {
  display: none;
}

.project-import-file-name {
  min-width: 180px;
  max-width: 320px;
  overflow: hidden;
  color: var(--asset-page-text-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-import-hint {
  margin-bottom: 12px;
  font-size: 13px;
  line-height: 20px;
  color: var(--asset-page-muted);
}
</style>
