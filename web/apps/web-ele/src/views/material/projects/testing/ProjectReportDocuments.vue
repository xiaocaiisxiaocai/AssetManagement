<script lang="ts" setup>
import type { ProjectDocumentDto } from '#/api/project-workspace';

import { computed, ref } from 'vue';

import { ElButton, ElPopover } from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

import { validateProjectDocument } from './project-testing-rules';

const props = defineProps<{
  canDownload: boolean;
  canManage: boolean;
  deletingDocumentId: null | number;
  documents: ProjectDocumentDto[];
  downloadingKey: string;
  scopeName: string;
  stageId: number;
  uploading: boolean;
  writeBusy: boolean;
}>();

const emit = defineEmits<{
  download: [document: ProjectDocumentDto];
  remove: [document: ProjectDocumentDto];
  upload: [file: File, stageId: number, description: string];
}>();

const fileInput = ref<HTMLInputElement>();
const fileError = ref('');
let selectedScope: null | number | undefined;
const documents = computed(() =>
  props.documents
    .filter((document) => document.stageId === props.stageId)
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
  emit('upload', file, props.stageId, '');
}
</script>

<template>
  <div class="stage-report-cell">
    <div v-if="documents.length > 0" class="stage-report-links">
      <template v-for="document in documents.slice(0, 2)" :key="document.id">
        <ElButton
          v-if="canDownload"
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
        <span v-else :title="document.fileName" class="stage-report-file">
          {{ document.fileName }}
        </span>
      </template>
    </div>
    <div class="stage-report-actions">
      <ElButton
        v-if="canManage"
        :aria-label="'上传' + scopeName + '报告'"
        :disabled="writeBusy"
        :loading="uploading"
        link
        size="small"
        type="primary"
        @click="chooseFile"
      >
        上传报告
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
            附件（{{ documents.length }}）
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
              v-if="canDownload"
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
            <span v-else :title="document.fileName" class="stage-report-file">
              {{ document.fileName }}
            </span>
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
      :aria-label="'选择' + scopeName + '报告文件'"
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
