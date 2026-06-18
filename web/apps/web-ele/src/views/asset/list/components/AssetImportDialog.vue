<script lang="ts" setup>
import type { ImportPreviewRow } from '#/api/asset';

import { ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElMessage,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  confirmAssetImportApi,
  downloadAssetTemplateApi,
  validateAssetImportApi,
} from '#/api/asset';

const emit = defineEmits<{ imported: [] }>();

const visible = defineModel<boolean>('visible', { default: false });

const importing = ref(false);
const importPreview = ref<ImportPreviewRow[]>([]);
const selectedFile = ref<File | null>(null);

// 每次打开重置上次的选择与预览
watch(visible, (opened) => {
  if (opened) {
    selectedFile.value = null;
    importPreview.value = [];
  }
});

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

async function downloadTemplate() {
  const response = await downloadAssetTemplateApi();
  downloadBlob(response.data, 'asset-import-template.xlsx');
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  selectedFile.value = input.files?.[0] ?? null;
  importPreview.value = [];
}

async function validateImport() {
  if (!selectedFile.value) {
    ElMessage.warning('请先选择 Excel 文件');
    return;
  }
  importing.value = true;
  try {
    importPreview.value = await validateAssetImportApi(selectedFile.value);
  } finally {
    importing.value = false;
  }
}

async function confirmImport() {
  if (!selectedFile.value) {
    ElMessage.warning('请先选择 Excel 文件');
    return;
  }
  importing.value = true;
  try {
    const result = await confirmAssetImportApi(selectedFile.value);
    importPreview.value = result.rows;
    ElMessage.success(
      `导入成功 ${result.successCount} 条，失败 ${result.failedCount} 条`,
    );
    emit('imported');
  } finally {
    importing.value = false;
  }
}
</script>

<template>
  <ElDialog v-model="visible" title="批量导入资产" width="760px">
    <div class="space-y-3">
      <div class="flex flex-wrap items-center gap-2">
        <ElButton @click="downloadTemplate">下载模板</ElButton>
        <input accept=".xlsx" type="file" @change="onFileChange" />
        <ElButton :loading="importing" @click="validateImport">预校验</ElButton>
        <ElButton
          :disabled="!importPreview.some((row) => row.isValid)"
          :loading="importing"
          type="primary"
          @click="confirmImport"
        >
          确认导入
        </ElButton>
      </div>
      <ElTable :data="importPreview" border max-height="360">
        <ElTableColumn label="行号" prop="row" width="80" />
        <ElTableColumn label="名称" prop="name" />
        <ElTableColumn label="分类编码" prop="categoryCode" />
        <ElTableColumn label="单价" prop="price" width="100" />
        <ElTableColumn label="状态" width="90">
          <template #default="{ row }">
            <ElTag :type="row.isValid ? 'success' : 'danger'">
              {{ row.isValid ? '有效' : '无效' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="错误" min-width="180" prop="error" />
      </ElTable>
    </div>
  </ElDialog>
</template>
