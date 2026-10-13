<script lang="ts" setup>
import type { MaterialItem } from '#/api/material';

import {
  ElButton,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
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
  canEdit: boolean;
  currentUserId: number;
  isSupervisor: boolean;
  projectOwnerId?: null | number;
  row: MaterialItem;
}>();

const emit = defineEmits<{
  command: [command: number | string];
  detail: [];
  edit: [];
}>();

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
</script>

<template>
  <div class="material-row-actions">
    <template v-if="!row.isDeleted">
      <ElButton link size="small" type="primary" @click="emit('detail')">
        详情
      </ElButton>
      <ElButton
        v-if="canEdit && canOperate(row)"
        link
        size="small"
        type="primary"
        @click="emit('edit')"
      >
        编辑
      </ElButton>
      <ElDropdown
        v-if="
          canStartTransfer(row) ||
          (access.canReturn && canOperate(row) && !row.hasPendingFlow) ||
          (access.canDelete && canOperate(row))
        "
        @command="(command) => emit('command', command)"
      >
        <ElButton link size="small" type="primary">更多</ElButton>
        <template #dropdown>
          <ElDropdownMenu>
            <ElDropdownItem v-if="canStartTransfer(row)" command="transfer">
              转移
            </ElDropdownItem>
            <ElDropdownItem
              v-if="access.canReturn && canOperate(row) && !row.hasPendingFlow"
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
      <ElButton link size="small" type="primary" @click="emit('detail')">
        详情
      </ElButton>
      <ElDropdown
        v-if="access.canRestore || access.canPurge"
        @command="(command) => emit('command', command)"
      >
        <ElButton link size="small" type="primary">更多</ElButton>
        <template #dropdown>
          <ElDropdownMenu>
            <ElDropdownItem v-if="access.canRestore" command="restore">
              撤销删除
            </ElDropdownItem>
            <ElDropdownItem v-if="access.canPurge" command="purge" divided>
              彻底删除
            </ElDropdownItem>
          </ElDropdownMenu>
        </template>
      </ElDropdown>
    </template>
  </div>
</template>

<style scoped src="./ProjectMaterialsTab.css"></style>
