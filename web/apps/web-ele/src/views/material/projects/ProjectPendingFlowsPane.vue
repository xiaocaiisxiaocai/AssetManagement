<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
} from 'element-plus';

import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';

defineProps<{
  pageSizeOptions: number[];
  pendingCount: number;
  pendingFlows: MaterialFlowItem[];
  pendingLoading: boolean;
  processingFlowIds: number[];
}>();

const emit = defineEmits<{
  approve: [flow: MaterialFlowItem];
  pageChange: [];
  pendingPageSizeChange: [];
  reject: [flow: MaterialFlowItem];
  release: [flow: MaterialFlowItem];
}>();
const pendingQuery = defineModel<{ page: number; pageSize: number }>(
  'pendingQuery',
  { required: true },
);
</script>

<template>
  <div class="drawer-table-panel flow-table-panel">
    <ElTable
      :data="pendingFlows"
      :empty-text="pendingCount > 0 ? '当前页暂无待审批流转' : '暂无待审批流转'"
      border
      height="100%"
      stripe
      v-loading="pendingLoading"
    >
      <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
      <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
      <ElTableColumn
        label="料件名称"
        min-width="140"
        prop="materialName"
        show-overflow-tooltip
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="发起人"
        min-width="90"
        prop="applicant"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="受让人"
        min-width="90"
        prop="transferee"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="原因"
        min-width="150"
        prop="reason"
        show-overflow-tooltip
      />
      <ElTableColumn label="审批进度" min-width="320">
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.currentSteps"
            :next-steps="row.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        align="center"
        class-name="table-action-cell"
        label="操作"
        width="168"
      >
        <template #default="{ row }">
          <ElButton
            v-if="row.canReleaseStuck"
            :disabled="processingFlowIds.includes(row.id)"
            link
            size="small"
            type="danger"
            @click="emit('release', row)"
          >
            终止并重新发起
          </ElButton>
          <template v-else>
            <ElButton
              :disabled="
                row.actionableNodeIds.length === 0 ||
                processingFlowIds.includes(row.id)
              "
              link
              size="small"
              type="success"
              @click="emit('approve', row)"
            >
              通过
            </ElButton>
            <ElButton
              :disabled="
                row.actionableNodeIds.length === 0 ||
                processingFlowIds.includes(row.id)
              "
              link
              size="small"
              type="danger"
              @click="emit('reject', row)"
            >
              驳回
            </ElButton>
          </template>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ pendingCount }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="pendingQuery.pageSize"
          style="width: 92px"
          @change="emit('pendingPageSizeChange')"
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
        v-model:current-page="pendingQuery.page"
        :page-size="pendingQuery.pageSize"
        :total="pendingCount"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
