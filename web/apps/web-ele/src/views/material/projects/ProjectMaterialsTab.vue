<script lang="ts" setup>
import type { DeleteStatus } from './project-workspace-types';

import type { MaterialItem, MaterialStatus } from '#/api/material';

import {
  ElButton,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
  ElInput,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTabPane,
  ElTag,
} from 'element-plus';

import { canTransferMaterial } from './material-row-actions';

type MaterialActionAccess = {
  canDelete: boolean;
  canPurge: boolean;
  canRestore: boolean;
  canReturn: boolean;
  canTransfer: boolean;
};

const props = defineProps<{
  access: MaterialActionAccess;
  canCreate: boolean;
  canEdit: boolean;
  currentUserId: number;
  isSupervisor: boolean;
  loading: boolean;
  materials: MaterialItem[];
  pageSizeOptions: number[];
  projectOwnerId?: null | number;
  total: number;
}>();

const emit = defineEmits<{
  command: [command: number | string, material: MaterialItem];
  create: [];
  detail: [material: MaterialItem];
  edit: [material: MaterialItem];
  pageChange: [];
  pageSizeChange: [];
  reset: [];
  search: [];
}>();
const query = defineModel<{
  deleteStatus: DeleteStatus;
  materialNo: string;
  name: string;
  page: number;
  pageSize: number;
  status?: MaterialStatus;
}>('query', { required: true });

const statusOptions: Array<{
  label: string;
  tag: 'info' | 'success';
  value: MaterialStatus;
}> = [
  { label: '在用', tag: 'success', value: 0 },
  { label: '已退回厂商', tag: 'info', value: 1 },
];

function statusMeta(status: MaterialStatus) {
  return (
    statusOptions.find((item) => item.value === status) ?? statusOptions[0]!
  );
}

function canOperate(row: MaterialItem) {
  return !row.isDeleted && row.status !== 1;
}

function canStartTransfer(row: MaterialItem) {
  return (
    props.access.canTransfer &&
    canTransferMaterial(row, {
      currentUserId: props.currentUserId,
      isSupervisor: props.isSupervisor,
      projectOwnerId: props.projectOwnerId,
    })
  );
}

function rowClassName({ row }: { row: MaterialItem }) {
  if (row.isDeleted) return 'material-row-deleted';
  return row.status === 1 ? 'material-row-returned' : '';
}
</script>

<template>
  <ElTabPane name="materials">
    <template #label>
      <span>料件清单</span><span class="tab-count">{{ total }}</span>
    </template>
    <div class="material-filter">
      <ElInput
        v-model="query.materialNo"
        aria-label="料件编号"
        clearable
        placeholder="料件编号"
        style="width: 150px"
        @keyup.enter="emit('search')"
      />
      <ElInput
        v-model="query.name"
        aria-label="料件名称"
        clearable
        placeholder="料件名称"
        style="width: 160px"
        @keyup.enter="emit('search')"
      />
      <ElSelect
        v-model="query.status"
        aria-label="料件状态"
        clearable
        placeholder="状态"
        style="width: 120px"
      >
        <ElOption
          v-for="item in statusOptions"
          :key="item.value"
          :label="item.label"
          :value="item.value"
        />
      </ElSelect>
      <ElSelect
        v-model="query.deleteStatus"
        aria-label="料件删除状态"
        placeholder="删除状态"
        style="width: 120px"
        @change="emit('search')"
      >
        <ElOption label="全部" value="all" />
        <ElOption label="未删除" value="active" />
        <ElOption label="已删除" value="deleted" />
      </ElSelect>
      <ElButton type="primary" @click="emit('search')">查询</ElButton>
      <ElButton @click="emit('reset')">重置</ElButton>
      <ElButton v-if="canCreate" type="primary" @click="emit('create')">
        新增料件
      </ElButton>
    </div>
    <div class="drawer-table-panel material-table-panel">
      <ElTable
        :data="materials"
        :row-class-name="rowClassName"
        border
        height="100%"
        scrollbar-always-on
        stripe
        v-loading="loading"
      >
        <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
        <ElTableColumn
          label="名称"
          min-width="140"
          prop="name"
          show-overflow-tooltip
        />
        <ElTableColumn
          label="厂商"
          min-width="120"
          prop="vendorName"
          show-overflow-tooltip
        />
        <ElTableColumn label="型号品牌" min-width="140" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.model || row.brand">
              {{ row.model }} {{ row.brand }}
            </span>
            <span v-else>-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="数量" prop="quantity" width="70" />
        <ElTableColumn
          label="部门"
          min-width="110"
          prop="departmentName"
          show-overflow-tooltip
        />
        <ElTableColumn
          label="保管人"
          min-width="100"
          prop="custodianName"
          show-overflow-tooltip
        />
        <ElTableColumn label="备注" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">{{ row.remark || '-' }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="状态" width="130">
          <template #default="{ row }">
            <ElTag :type="statusMeta(row.status).tag" size="small">
              {{ statusMeta(row.status).label }}
            </ElTag>
            <ElTag v-if="row.isDeleted" class="ml-1" size="small" type="danger">
              已删除
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" fixed="right" label="操作" width="150">
          <template #default="{ row }">
            <div class="material-row-actions">
              <template v-if="!row.isDeleted">
                <ElButton
                  link
                  size="small"
                  type="primary"
                  @click="emit('detail', row)"
                >
                  详情
                </ElButton>
                <ElButton
                  v-if="canEdit && canOperate(row)"
                  link
                  size="small"
                  type="primary"
                  @click="emit('edit', row)"
                >
                  编辑
                </ElButton>
                <ElDropdown
                  v-if="
                    canStartTransfer(row) ||
                    (access.canReturn &&
                      canOperate(row) &&
                      !row.hasPendingFlow) ||
                    (access.canDelete && canOperate(row))
                  "
                  @command="(command) => emit('command', command, row)"
                >
                  <ElButton link size="small" type="primary">更多</ElButton>
                  <template #dropdown>
                    <ElDropdownMenu>
                      <ElDropdownItem
                        v-if="canStartTransfer(row)"
                        command="transfer"
                      >
                        转移
                      </ElDropdownItem>
                      <ElDropdownItem
                        v-if="
                          access.canReturn &&
                          canOperate(row) &&
                          !row.hasPendingFlow
                        "
                        command="return"
                      >
                        退回厂商
                      </ElDropdownItem>
                      <ElDropdownItem
                        v-if="access.canDelete && canOperate(row)"
                        command="delete"
                        divided
                      >
                        删除
                      </ElDropdownItem>
                    </ElDropdownMenu>
                  </template>
                </ElDropdown>
              </template>
              <template v-else>
                <ElButton
                  link
                  size="small"
                  type="primary"
                  @click="emit('detail', row)"
                >
                  详情
                </ElButton>
                <ElDropdown
                  v-if="access.canRestore || access.canPurge"
                  @command="(command) => emit('command', command, row)"
                >
                  <ElButton link size="small" type="primary">更多</ElButton>
                  <template #dropdown>
                    <ElDropdownMenu>
                      <ElDropdownItem
                        v-if="access.canRestore"
                        command="restore"
                      >
                        撤销删除
                      </ElDropdownItem>
                      <ElDropdownItem
                        v-if="access.canPurge"
                        command="purge"
                        divided
                      >
                        彻底删除
                      </ElDropdownItem>
                    </ElDropdownMenu>
                  </template>
                </ElDropdown>
              </template>
            </div>
          </template>
        </ElTableColumn>
      </ElTable>
      <div class="table-bottom-pager">
        <div class="table-bottom-pager-left">
          <span>共 {{ total }} 条记录</span>
          <span class="table-bottom-pager-divider">|</span>
          <span>每页</span>
          <ElSelect
            v-model="query.pageSize"
            aria-label="料件列表每页条数"
            style="width: 92px"
            @change="emit('pageSizeChange')"
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
          v-model:current-page="query.page"
          :page-size="query.pageSize"
          :total="total"
          background
          layout="prev, pager, next"
          @current-change="emit('pageChange')"
        />
      </div>
    </div>
  </ElTabPane>
</template>

<style scoped>
.tab-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 18px;
  padding: 0 6px;
  margin-left: 6px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
  background: var(--el-fill-color-light);
  border-radius: 999px;
}
.material-filter {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  margin-bottom: 14px;
}
.drawer-table-panel {
  display: flex;
  flex: 1;
  min-height: 0;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  background: var(--asset-page-surface);
}
.drawer-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
}
.material-table-panel {
  flex: 1;
  height: auto;
  min-height: 0;
}
.material-row-actions {
  display: flex;
  gap: 4px;
  align-items: center;
  justify-content: center;
}
.material-row-actions :deep(.el-button + .el-button) {
  margin-left: 0;
}
.table-bottom-pager {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-top: 1px solid var(--asset-page-border);
  background: var(--asset-page-surface);
}
.table-bottom-pager-left {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  color: var(--asset-page-muted);
  font-size: 14px;
  line-height: 20px;
}
.table-bottom-pager-divider {
  color: var(--asset-page-border);
}
:deep(.material-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: var(--el-fill-color-light) !important;
}
:deep(.material-row-returned td.el-table__cell) {
  color: var(--el-text-color-secondary);
}
@media (max-width: 768px) {
  .material-table-panel {
    height: auto;
  }
}
</style>
