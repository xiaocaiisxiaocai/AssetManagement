<script lang="ts" setup>
import type { AssetListQueryState } from '../asset-list-state';
import type { AssetRowActionAccess } from '../asset-row-actions';

import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import {
  ElButton,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
} from 'element-plus';

import {
  canBorrowAvailableAsset,
  canRunAvailableAssetAction,
  canTransferAvailableAsset,
} from '../asset-row-actions';
import AssetFilterStrip from './AssetFilterStrip.vue';
import AssetListTable from './AssetListTable.vue';

defineProps<{
  access: AssetRowActionAccess;
  assets: AssetItem[];
  canPurgeAsset: boolean;
  canRestoreAsset: boolean;
  currentUserId: number;
  deletingAssetIds: number[];
  departmentOptions: { id: number; label: string }[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
  userOptionsLoading: boolean;
  users: UserOptionDto[];
}>();

const emit = defineEmits<{
  command: [command: string, row: AssetItem];
  detail: [row: AssetItem];
  edit: [row: AssetItem];
  pageChange: [];
  reset: [];
  search: [];
  userSearch: [keyword: string];
}>();

const query = defineModel<AssetListQueryState>('query', { required: true });
</script>

<template>
  <AssetFilterStrip
    v-model:query="query"
    :department-options="departmentOptions"
    :user-options-loading="userOptionsLoading"
    :users="users"
    @reset="emit('reset')"
    @search="emit('search')"
    @user-search="emit('userSearch', $event)"
  />
  <AssetListTable
    v-model:query="query"
    :assets="assets"
    :loading="loading"
    :page-size-options="pageSizeOptions"
    :total="total"
    @page-change="emit('pageChange')"
    @search="emit('search')"
  >
    <template #actions="{ row }">
      <div class="asset-row-actions">
        <template v-if="!row.isDeleted">
          <ElButton
            v-if="access.canView"
            link
            size="small"
            type="primary"
            @click="emit('detail', row)"
          >
            详情
          </ElButton>
          <ElButton
            v-if="access.canEdit && row.canManage"
            link
            size="small"
            type="primary"
            @click="emit('edit', row)"
          >
            编辑
          </ElButton>
          <ElDropdown
            v-if="
              (access.canBorrow &&
                canBorrowAvailableAsset(row, currentUserId)) ||
              (access.canDelete &&
                row.canManage &&
                canRunAvailableAssetAction(row)) ||
              (access.canTransfer &&
                canTransferAvailableAsset(row, currentUserId))
            "
            @command="(cmd) => emit('command', String(cmd), row)"
          >
            <ElButton link size="small" type="primary"> 更多 </ElButton>
            <template #dropdown>
              <ElDropdownMenu>
                <ElDropdownItem
                  v-if="
                    access.canBorrow &&
                    canBorrowAvailableAsset(row, currentUserId)
                  "
                  command="borrow"
                >
                  借用
                </ElDropdownItem>
                <ElDropdownItem
                  v-if="
                    access.canTransfer &&
                    canTransferAvailableAsset(row, currentUserId)
                  "
                  command="transfer"
                >
                  转让
                </ElDropdownItem>
                <ElDropdownItem
                  v-if="
                    access.canDelete &&
                    row.canManage &&
                    canRunAvailableAssetAction(row)
                  "
                  :disabled="deletingAssetIds.includes(row.id)"
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
            v-if="access.canView"
            link
            size="small"
            type="primary"
            @click="emit('detail', row)"
          >
            详情
          </ElButton>
          <ElDropdown
            v-if="row.canManage && (canRestoreAsset || canPurgeAsset)"
            @command="(cmd) => emit('command', String(cmd), row)"
          >
            <ElButton link size="small" type="primary"> 更多 </ElButton>
            <template #dropdown>
              <ElDropdownMenu>
                <ElDropdownItem
                  v-if="canRestoreAsset && row.canManage"
                  command="restore"
                >
                  撤销删除
                </ElDropdownItem>
                <ElDropdownItem
                  v-if="canPurgeAsset && row.canManage"
                  command="purge"
                  divided
                >
                  彻底删除
                </ElDropdownItem>
              </ElDropdownMenu>
            </template>
          </ElDropdown>
          <span
            v-if="!row.canManage || (!canRestoreAsset && !canPurgeAsset)"
            class="asset-no-permission"
          >
            无操作权限
          </span>
        </template>
      </div>
    </template>
  </AssetListTable>
</template>

<style scoped src="./AssetTablePanel.css"></style>
