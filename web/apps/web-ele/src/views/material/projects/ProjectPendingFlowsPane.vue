<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';
import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';

defineProps<{
  /** 项目已锁定或删除时只查看，不在工作台内处理。 */
  canHandle: boolean;
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

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流转单号' },
  { key: 'materialNo', label: '料件编号' },
  { key: 'materialName', label: '料件名称' },
  { key: 'applicant', label: '发起人' },
  { key: 'transferee', label: '受让人' },
  { key: 'reason', label: '原因' },
  { key: 'progress', label: '审批进度' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-flows-pending', tableColumns);
</script>

<template>
  <div class="flow-pane">
    <Teleport defer to="[data-column-tools='project-flows']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        size="small"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <div class="drawer-table-panel flow-table-panel">
      <div class="flow-fill-table">
        <ElTable
          :data="pendingFlows"
          :empty-text="
            pendingCount > 0 ? '当前页暂无待审批流转' : '暂无待审批流转'
          "
          border
          height="100%"
          stripe
          v-loading="pendingLoading"
        >
          <ElTableColumn
            v-if="visibleColumn('flowNo')"
            label="流转单号"
            prop="flowNo"
            show-overflow-tooltip
            width="148"
          />
          <ElTableColumn
            v-if="visibleColumn('materialNo')"
            label="料件编号"
            prop="materialNo"
            show-overflow-tooltip
            width="148"
          />
          <ElTableColumn
            v-if="visibleColumn('materialName')"
            label="料件名称"
            min-width="108"
            prop="materialName"
            show-overflow-tooltip
          />
          <ElTableColumn
            v-if="visibleColumn('applicant')"
            class-name="hide-on-mobile"
            label="发起人"
            prop="applicant"
            show-overflow-tooltip
            width="84"
          />
          <ElTableColumn
            v-if="visibleColumn('transferee')"
            class-name="hide-on-mobile"
            label="受让人"
            prop="transferee"
            show-overflow-tooltip
            width="84"
          />
          <ElTableColumn
            v-if="visibleColumn('reason')"
            class-name="hide-on-mobile"
            label="原因"
            min-width="108"
            prop="reason"
            show-overflow-tooltip
          />
          <ElTableColumn
            v-if="visibleColumn('progress')"
            label="审批进度"
            width="168"
          >
            <template #default="{ row }">
              <WorkflowProgressSummary
                :current-steps="row.currentSteps"
                :next-steps="row.nextSteps"
                :status="row.status"
              />
            </template>
          </ElTableColumn>
          <ElTableColumn
            v-if="canHandle && visibleColumn('actions')"
            align="center"
            class-name="table-action-cell"
            label="操作"
            width="128"
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
      </div>
      <div
        v-if="pendingCount > pendingQuery.pageSize"
        class="table-bottom-pager"
      >
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
  </div>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
