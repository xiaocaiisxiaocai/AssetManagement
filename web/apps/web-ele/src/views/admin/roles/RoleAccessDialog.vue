<script lang="ts" setup>
import type { PermissionGroup } from './permission-groups';

import type { MenuDto, PermissionDto } from '#/api/role';

import { computed, nextTick, ref, watch } from 'vue';

import { ElAlert, ElButton, ElDialog } from 'element-plus';

import RoleMenuPanel from './RoleMenuPanel.vue';
import RolePermissionPanel from './RolePermissionPanel.vue';

const props = defineProps<{
  activePermissionGroupKeyValue: string;
  activePermissionGroupLabel: string;
  checkedLeafMenuIds: number[];
  filteredPermissions: PermissionDto[];
  pageMenuTree: MenuDto[];
  permissionCount: number;
  permissionGroups: PermissionGroup[];
  requiredPermissionIds: number[];
  roleName: string;
  saving: boolean;
  selectedMenuCount: number;
}>();

const emit = defineEmits<{
  clearCurrent: [];
  save: [];
  selectCurrent: [];
  selectGroup: [key: string];
  treeCheck: [payload: { checkedKeys: number[]; halfCheckedKeys: number[] }];
}>();

const visible = defineModel<boolean>('visible', { required: true });
const selectedPermissions = defineModel<number[]>('selectedPermissions', {
  required: true,
});
const permissionKeyword = defineModel<string>('permissionKeyword', {
  required: true,
});
const showSelectedOnly = defineModel<boolean>('showSelectedOnly', {
  required: true,
});
const menuPanel = ref<InstanceType<typeof RoleMenuPanel>>();
const requiredPermissionIdSet = computed(
  () => new Set(props.requiredPermissionIds),
);

const actionLabelMap: Record<string, string> = {
  approve: '审批',
  assign: '分配',
  create: '新增',
  delete: '删除',
  design: '设计',
  edit: '编辑',
  export: '导出',
  followup: '跟进',
  handle: '处理',
  import: '导入',
  manage: '管理',
  option: '选项',
  purge: '彻底删除',
  remind: '提醒',
  restore: '恢复',
  return: '退回',
  transfer: '流转',
  upload: '上传',
  view: '查看',
};

function actionLabel(code: string) {
  const parts = code.split(':');
  const action = parts[parts.length - 1] ?? code;
  if (action.includes('-')) {
    return action
      .split('-')
      .map((part) => actionLabelMap[part] ?? part)
      .join('/');
  }
  return actionLabelMap[action] ?? action;
}

function syncTreeSelection() {
  const selection = menuPanel.value?.readSelection();
  emit('treeCheck', selection ?? { checkedKeys: [], halfCheckedKeys: [] });
}

function onSave() {
  syncTreeSelection();
  emit('save');
}

watch(visible, async (opened) => {
  if (!opened) return;
  await nextTick();
  menuPanel.value?.setCheckedKeys(props.checkedLeafMenuIds);
});
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="`授权配置 · ${roleName}`"
    class="role-access-dialog"
    width="min(1180px, calc(100vw - 24px))"
  >
    <ElAlert
      :closable="false"
      class="role-access-alert"
      show-icon
      title="菜单决定用户能看到哪些入口；功能权限决定进入后能执行哪些操作。勾选菜单时会自动补齐该页面的最低访问权限。"
      type="info"
    />

    <div class="role-access-shell">
      <RoleMenuPanel
        ref="menuPanel"
        :page-menu-tree="pageMenuTree"
        :selected-menu-count="selectedMenuCount"
      />
      <RolePermissionPanel
        v-model:permission-keyword="permissionKeyword"
        v-model:selected-permissions="selectedPermissions"
        v-model:show-selected-only="showSelectedOnly"
        :action-label="actionLabel"
        :active-permission-group-key-value="activePermissionGroupKeyValue"
        :active-permission-group-label="activePermissionGroupLabel"
        :filtered-permissions="filteredPermissions"
        :permission-count="permissionCount"
        :permission-groups="permissionGroups"
        :required-permission-id-set="requiredPermissionIdSet"
        @clear-current="emit('clearCurrent')"
        @select-current="emit('selectCurrent')"
        @select-group="emit('selectGroup', $event)"
      />
    </div>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="onSave">
        保存授权
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./RoleAccessDialog.css"></style>
