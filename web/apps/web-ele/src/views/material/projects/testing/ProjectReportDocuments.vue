<script lang="ts" setup>
import type { ProjectDocumentDto } from '#/api/project-workspace';

import { computed, ref } from 'vue';

import { ElButton, ElInput, ElPopover } from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

import { validateProjectDocument } from './project-testing-rules';

const props = defineProps<{
  canManage: boolean;
  compact?: boolean;
  deletingDocumentId: null | number;
  documents: ProjectDocumentDto[];
  downloadingKey: string;
  scopeName: string;
  stageId: null | number;
  uploading: boolean;
  writeBusy: boolean;
}>();

const emit = defineEmits<{
  download: [document: ProjectDocumentDto];
  remove: [document: ProjectDocumentDto];
  upload: [file: File, stageId: null | number, description: string];
}>();

const fileInput = ref<HTMLInputElement>();
const fileError = ref('');
const uploadDescription = ref('');
let selectedScope: null | number | undefined;
const documents = computed(() =>
  props.documents
    .filter((document) => (document.stageId ?? null) === props.stageId)
    .sort((left, right) => right.uploadedAt.localeCompare(left.uploadedAt)),
);

function chooseFile() {
  if (!props.canManage || props.writeBusy) return;
  selectedScope = props.stageId;
  fileError.value = '';
  fileInput.value?.click();
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (
    !file ||
    selectedScope !== props.stageId ||
    !props.canManage ||
    props.writeBusy
  )
    return;
  fileError.value = validateProjectDocument(file) ?? '';
  if (fileError.value) return;
  emit('upload', file, props.stageId, uploadDescription.value.trim());
  uploadDescription.value = '';
}
</script>

<template>
  <div :class="{ 'is-compact': compact }" class="stage-report-cell">
    <div v-if="!compact && documents.length > 0" class="stage-report-links">
      <ElButton
        v-for="document in documents.slice(0, 2)"
        :key="document.id"
        :disabled="Boolean(downloadingKey)"
        :loading="downloadingKey === `document-${document.id}`"
        :title="document.fileName"
        class="stage-report-file"
        link
        size="small"
        type="primary"
        @click="emit('download', document)"
      >
        {{ document.fileName }}
      </ElButton>
    </div>
    <div class="stage-report-actions">
      <ElPopover
        v-if="canManage"
        :width="300"
        placement="bottom"
        trigger="click"
      >
        <div class="stage-report-upload-popover">
          <ElInput
            v-model="uploadDescription"
            :maxlength="500"
            placeholder="可选：填写报告说明"
            show-word-limit
          />
        </div>
        <template #reference>
          <ElButton link size="small" type="primary"> 添加说明 </ElButton>
        </template>
      </ElPopover>
      <ElButton
        v-if="canManage"
        :aria-label="compact ? '上传项目总报告' : `上传${scopeName}报告`"
        :disabled="writeBusy"
        :loading="uploading"
        link
        size="small"
        type="primary"
        @click="chooseFile"
      >
        {{ compact ? '上传总报告' : '上传报告' }}
      </ElButton>
      <ElPopover
        v-if="documents.length > 0"
        :popper-style="{ maxWidth: 'calc(100vw - 32px)' }"
        :width="420"
        placement="bottom"
        trigger="click"
      >
        <template #reference>
          <ElButton link size="small" type="primary">
            {{ compact ? '项目总报告' : '附件' }}（{{ documents.length }}）
          </ElButton>
        </template>
        <div class="stage-report-popover">
          <strong>{{ scopeName }} · 报告附件</strong>
          <div
            v-for="document in documents"
            :key="document.id"
            class="stage-report-item"
          >
            <ElButton
              :disabled="Boolean(downloadingKey)"
              :loading="downloadingKey === `document-${document.id}`"
              :title="document.fileName"
              class="stage-report-file"
              link
              size="small"
              type="primary"
              @click="emit('download', document)"
            >
              {{ document.fileName }}
            </ElButton>
            <div class="stage-report-meta">
              {{ document.uploadedBy }} ·
              {{ formatDateTime(document.uploadedAt) }}
            </div>
            <div v-if="document.description" class="stage-report-description">
              {{ document.description }}
            </div>
            <ElButton
              v-if="canManage"
              :disabled="writeBusy"
              :loading="deletingDocumentId === document.id"
              link
              size="small"
              type="danger"
              @click="emit('remove', document)"
            >
              删除
            </ElButton>
          </div>
        </div>
      </ElPopover>
      <span v-if="!canManage && documents.length === 0">—</span>
    </div>
    <input
      ref="fileInput"
      :aria-label="compact ? '选择项目总报告文件' : `选择${scopeName}报告文件`"
      accept=".pdf,.docx,.xlsx,.pptx,.png,.jpg,.jpeg"
      class="report-native-file"
      type="file"
      @change="onFileChange"
    />
    <span v-if="fileError" class="upload-error" role="alert">{{
      fileError
    }}</span>
  </div>
</template>

<style scoped src="./ProjectReportDocuments.css"></style>
