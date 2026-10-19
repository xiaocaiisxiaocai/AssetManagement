<script lang="ts" setup>
import type {
  ProjectDocumentDto,
  ProjectStageDto,
} from '#/api/project-workspace';

import { computed, ref } from 'vue';

import {
  ElAlert,
  ElButton,
  ElEmpty,
  ElInput,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTooltip,
} from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

import { validateProjectDocument } from './project-testing-rules';

const props = defineProps<{
  canManage: boolean;
  deletingDocumentId: null | number;
  documents: ProjectDocumentDto[];
  downloadingKey: string;
  stages: ProjectStageDto[];
  uploading: boolean;
  writeBusy: boolean;
}>();

const emit = defineEmits<{
  download: [document: ProjectDocumentDto];
  remove: [document: ProjectDocumentDto];
  upload: [
    file: File,
    stageId: null | number,
    description: string,
    onSuccess: () => void,
  ];
}>();

const fileInputRef = ref<HTMLInputElement>();
const selectedFile = ref<File | null>(null);
const selectedStageId = ref(0);
const description = ref('');
const fileError = ref('');

const sortedDocuments = computed(() =>
  [...props.documents].sort((left, right) =>
    right.uploadedAt.localeCompare(left.uploadedAt),
  ),
);
const stageNames = computed(
  () => new Map(props.stages.map((stage) => [stage.id, stage.name])),
);

function chooseFile() {
  if (!props.canManage || props.writeBusy) return;
  fileInputRef.value?.click();
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0] ?? null;
  fileError.value = file ? (validateProjectDocument(file) ?? '') : '';
  selectedFile.value = fileError.value ? null : file;
}

function clearUploadDraft() {
  selectedFile.value = null;
  selectedStageId.value = 0;
  description.value = '';
  fileError.value = '';
  if (fileInputRef.value) fileInputRef.value.value = '';
}

function submitUpload() {
  if (!selectedFile.value || props.writeBusy) return;
  emit(
    'upload',
    selectedFile.value,
    selectedStageId.value || null,
    description.value,
    clearUploadDraft,
  );
}

function documentScope(document: ProjectDocumentDto) {
  if (document.stageId === null || document.stageId === undefined)
    return '项目总报告';
  return stageNames.value.get(document.stageId) ?? `阶段 #${document.stageId}`;
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
</script>

<template>
  <section class="testing-section report-section">
    <header class="testing-section-header">
      <div>
        <h3>报告资料</h3>
        <p>阶段报告与项目总报告统一归档，单个文件最大 20MB。</p>
      </div>
      <ElTag size="small" type="info">{{ documents.length }} 份</ElTag>
    </header>

    <div v-if="canManage" class="report-upload-bar">
      <ElSelect
        v-model="selectedStageId"
        :disabled="writeBusy"
        aria-label="报告归属阶段"
        class="report-stage-select"
      >
        <ElOption :value="0" label="项目总报告" />
        <ElOption
          v-for="stage in stages"
          :key="stage.id"
          :label="`${stage.sequence}. ${stage.name}`"
          :value="stage.id"
        />
      </ElSelect>
      <ElInput
        v-model="description"
        :disabled="writeBusy"
        :maxlength="500"
        aria-label="报告说明"
        class="report-description-input"
        clearable
        placeholder="报告说明（可选）"
        show-word-limit
      />
      <input
        ref="fileInputRef"
        accept=".pdf,.docx,.xlsx,.pptx,.png,.jpg,.jpeg"
        class="report-native-file"
        type="file"
        @change="onFileChange"
      />
      <ElTooltip
        :content="selectedFile?.name || 'PDF/DOCX/XLSX/PPTX/PNG/JPG，最大 20MB'"
        placement="top"
      >
        <ElButton :disabled="writeBusy" @click="chooseFile">
          {{ selectedFile ? '重新选择' : '选择文件' }}
        </ElButton>
      </ElTooltip>
      <span :title="selectedFile?.name" class="selected-file-name">
        {{ selectedFile?.name || '未选择文件' }}
      </span>
      <ElButton
        :disabled="!selectedFile || writeBusy"
        :loading="uploading"
        type="primary"
        @click="submitUpload"
      >
        上传报告
      </ElButton>
      <span v-if="fileError" class="upload-error">{{ fileError }}</span>
    </div>
    <ElAlert
      v-else
      :closable="false"
      class="readonly-alert"
      show-icon
      title="当前为只读模式，可下载已有报告。"
      type="info"
    />

    <div class="report-table-wrap">
      <ElTable
        v-if="sortedDocuments.length > 0"
        :data="sortedDocuments"
        :max-height="360"
        border
        class="compact-table report-table"
        empty-text="暂无报告资料"
        table-layout="fixed"
      >
        <ElTableColumn align="center" label="文件名" min-width="220">
          <template #default="{ row }">
            <span :title="row.fileName" class="file-name-cell">
              {{ row.fileName }}
            </span>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="报告归属" min-width="150">
          <template #default="{ row }">{{ documentScope(row) }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="说明" min-width="200">
          <template #default="{ row }">
            <span :title="row.description || ''" class="description-cell">
              {{ row.description || '—' }}
            </span>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="大小" width="100">
          <template #default="{ row }">{{ formatSize(row.size) }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="上传人" min-width="110">
          <template #default="{ row }">{{ row.uploadedBy || '—' }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="上传时间" width="168">
          <template #default="{ row }">
            {{ formatDateTime(row.uploadedAt) }}
          </template>
        </ElTableColumn>
        <ElTableColumn
          align="center"
          class-name="table-action-cell"
          fixed="right"
          label="操作"
          width="150"
        >
          <template #default="{ row }">
            <div class="centered-actions">
              <ElButton
                :disabled="
                  Boolean(downloadingKey) &&
                  downloadingKey !== `document-${row.id}`
                "
                :loading="downloadingKey === `document-${row.id}`"
                link
                size="small"
                type="primary"
                @click="emit('download', row)"
              >
                下载
              </ElButton>
              <ElButton
                v-if="canManage"
                :disabled="writeBusy && deletingDocumentId !== row.id"
                :loading="deletingDocumentId === row.id"
                link
                size="small"
                type="danger"
                @click="emit('remove', row)"
              >
                删除
              </ElButton>
            </div>
          </template>
        </ElTableColumn>
      </ElTable>
      <ElEmpty v-else description="暂无报告资料" />
    </div>
  </section>
</template>

<style scoped src="./testing-workspace.css"></style>
