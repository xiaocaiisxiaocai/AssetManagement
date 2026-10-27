<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import { computed, ref } from 'vue';

import { ElTabPane, ElTabs } from 'element-plus';

import ProjectHandledFlowsPane from './ProjectHandledFlowsPane.vue';
import ProjectMyFlowsPane from './ProjectMyFlowsPane.vue';
import ProjectPendingFlowsPane from './ProjectPendingFlowsPane.vue';

const props = defineProps<{
  /** 项目处于可操作阶段时才能在工作台内通过、驳回。 */
  canHandle: boolean;
  /** 账号有审批权限时显示待我审批、我已处理。 */
  canViewApprovals: boolean;
  handledCount: number;
  handledFlows: MaterialFlowItem[];
  handledLoading: boolean;
  myCount: number;
  myFlows: MaterialFlowItem[];
  myLoading: boolean;
  pageSizeOptions: number[];
  pendingCount: number;
  pendingFlows: MaterialFlowItem[];
  pendingLoading: boolean;
  processingFlowIds: number[];
  readOnly: boolean;
}>();

const emit = defineEmits<{
  approve: [flow: MaterialFlowItem];
  handledPageSizeChange: [];
  myPageSizeChange: [];
  pageChange: [kind: 'handled' | 'mine' | 'pending'];
  pendingPageSizeChange: [];
  reject: [flow: MaterialFlowItem];
  release: [flow: MaterialFlowItem];
  withdraw: [flow: MaterialFlowItem];
}>();
const handledQuery = defineModel<{ page: number; pageSize: number }>(
  'handledQuery',
  { required: true },
);
const myQuery = defineModel<{ page: number; pageSize: number }>('myQuery', {
  required: true,
});
const pendingQuery = defineModel<{ page: number; pageSize: number }>(
  'pendingQuery',
  { required: true },
);
const activeTab = ref<'handled' | 'mine' | 'pending'>(
  props.canViewApprovals ? 'pending' : 'mine',
);
const currentTab = computed({
  get(): 'handled' | 'mine' | 'pending' {
    return props.canViewApprovals ? activeTab.value : 'mine';
  },
  set(value: 'handled' | 'mine' | 'pending') {
    activeTab.value = value;
  },
});
</script>

<template>
  <section aria-label="流转审批" class="workspace-section">
    <div class="workspace-section-heading">
      <h3>流转审批</h3>
      <ElTabs v-model="currentTab" class="flow-tabs">
        <ElTabPane v-if="canViewApprovals" name="pending">
          <template #label>
            待我审批
            <span
              :class="{ 'is-warning': pendingCount > 0 }"
              class="section-count"
            >
              {{ pendingCount }}
            </span>
          </template>
        </ElTabPane>
        <ElTabPane v-if="canViewApprovals" name="handled">
          <template #label>
            我已处理
            <span class="section-count">{{ handledCount }}</span>
          </template>
        </ElTabPane>
        <ElTabPane name="mine">
          <template #label>
            我的发起
            <span class="section-count">{{ myCount }}</span>
          </template>
        </ElTabPane>
      </ElTabs>
      <div
        class="workspace-section-actions"
        data-column-tools="project-flows"
      ></div>
    </div>
    <div class="workspace-section-body workspace-flow-body">
      <ProjectPendingFlowsPane
        v-if="currentTab === 'pending'"
        v-model:pending-query="pendingQuery"
        :can-handle="canHandle"
        :page-size-options="pageSizeOptions"
        :pending-count="pendingCount"
        :pending-flows="pendingFlows"
        :pending-loading="pendingLoading"
        :processing-flow-ids="processingFlowIds"
        @approve="emit('approve', $event)"
        @page-change="emit('pageChange', 'pending')"
        @pending-page-size-change="emit('pendingPageSizeChange')"
        @reject="emit('reject', $event)"
        @release="emit('release', $event)"
      />
      <ProjectHandledFlowsPane
        v-else-if="currentTab === 'handled'"
        v-model:handled-query="handledQuery"
        :handled-count="handledCount"
        :handled-flows="handledFlows"
        :handled-loading="handledLoading"
        :page-size-options="pageSizeOptions"
        @handled-page-size-change="emit('handledPageSizeChange')"
        @page-change="emit('pageChange', 'handled')"
      />
      <ProjectMyFlowsPane
        v-else
        v-model:my-query="myQuery"
        :my-count="myCount"
        :my-flows="myFlows"
        :my-loading="myLoading"
        :page-size-options="pageSizeOptions"
        :processing-flow-ids="processingFlowIds"
        :read-only="readOnly"
        @my-page-size-change="emit('myPageSizeChange')"
        @page-change="emit('pageChange', 'mine')"
        @withdraw="emit('withdraw', $event)"
      />
    </div>
  </section>
</template>

<style scoped src="./project-workspace-section.css"></style>
<style scoped src="./ProjectFlowsTab.css"></style>
