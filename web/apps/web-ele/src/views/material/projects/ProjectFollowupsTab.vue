<script lang="ts" setup>
import type { TestProjectFollowup, TestProjectItem } from '#/api/test-project';

import { computed } from 'vue';

import {
  ElButton,
  ElEmpty,
  ElInput,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

const props = defineProps<{
  editingId: null | number;
  followups: TestProjectFollowup[];
  loading: boolean;
  project: TestProjectItem;
  saving: boolean;
}>();
const emit = defineEmits<{
  cancelEdit: [];
  edit: [followup: TestProjectFollowup];
  save: [];
}>();
const form = defineModel<{ content: string }>('form', { required: true });
const blockedByTesting = computed(
  () => props.project.testingIncomplete === true,
);
const canWrite = computed(
  () =>
    props.project.canWriteFollowUp &&
    !props.project.isDeleted &&
    !blockedByTesting.value,
);
</script>

<template>
  <section aria-label="落地跟进" class="workspace-section">
    <div class="workspace-section-heading">
      <h3>
        落地跟进
        <span class="section-count">{{ followups.length }}</span>
      </h3>
      <ElTag v-if="blockedByTesting" size="small" type="warning">
        阶段未完成
      </ElTag>
      <ElTag v-else-if="!canWrite" size="small" type="info">只读</ElTag>
      <ElTag v-else-if="editingId" size="small" type="primary">编辑中</ElTag>
    </div>
    <div class="workspace-section-body followup-workspace">
      <div class="followup-editor-panel">
        <template v-if="canWrite">
          <ElInput
            v-model="form.content"
            :autosize="{ minRows: 1, maxRows: 3 }"
            :disabled="saving"
            aria-label="落地情况"
            class="followup-input"
            maxlength="2000"
            placeholder="填写本周期落地进展、问题和下一步"
            resize="none"
            show-word-limit
            type="textarea"
          />
          <div class="followup-actions">
            <ElButton
              v-if="editingId"
              :disabled="saving"
              @click="emit('cancelEdit')"
            >
              取消编辑
            </ElButton>
            <ElButton :loading="saving" type="primary" @click="emit('save')">
              {{ editingId ? '保存修改' : '新增跟进' }}
            </ElButton>
          </div>
        </template>
        <div v-else class="readonly-note">
          {{
            project.isDeleted
              ? '项目已删除，仅保留历史记录供查看。'
              : blockedByTesting
                ? '测试阶段仍在进行或尚未填写结束日期，完成后再填写落地跟进。'
                : project.closedDate
                  ? '项目已结案，跟进记录已锁定。'
                  : '项目进入落地跟进后，负责人或管理员才能填写。'
          }}
        </div>
      </div>
      <section class="followup-history-panel" v-loading="loading">
        <div
          :class="{ 'is-empty': followups.length === 0 && !loading }"
          class="followup-history-scroll"
        >
          <ElEmpty
            v-if="followups.length === 0 && !loading"
            :image-size="40"
            class="compact-empty"
            description="暂无跟进记录"
          />
          <ElTimeline v-else class="followup-timeline">
            <ElTimelineItem
              v-for="item in followups"
              :key="item.id"
              placement="top"
            >
              <div class="followup-record-heading">
                <div class="followup-record-meta">
                  <span>{{ item.filledByName || '-' }}</span>
                  <span
                    >跟进时间
                    {{ formatDateTime(item.filledAt, { seconds: true }) }}</span
                  >
                </div>
                <div v-if="canWrite && item.isLatest" class="record-actions">
                  <ElButton
                    :disabled="saving"
                    link
                    size="small"
                    type="primary"
                    @click="emit('edit', item)"
                  >
                    编辑
                  </ElButton>
                </div>
              </div>
              <article class="followup-record">
                <div class="followup-content">{{ item.content }}</div>
              </article>
            </ElTimelineItem>
          </ElTimeline>
        </div>
      </section>
    </div>
  </section>
</template>

<style scoped src="./project-workspace-section.css"></style>
<style scoped src="./ProjectFollowupsTab.css"></style>
