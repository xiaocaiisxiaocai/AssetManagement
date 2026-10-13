<script lang="ts" setup>
import type { PermissionGroup } from './permission-groups';

import type { PermissionDto } from '#/api/role';

import {
  ElButton,
  ElCheckbox,
  ElCheckboxGroup,
  ElEmpty,
  ElInput,
} from 'element-plus';

defineProps<{
  actionLabel: (code: string) => string;
  activePermissionGroupKeyValue: string;
  activePermissionGroupLabel: string;
  filteredPermissions: PermissionDto[];
  permissionCount: number;
  permissionGroups: PermissionGroup[];
  requiredPermissionIdSet: Set<number>;
}>();

const emit = defineEmits<{
  clearCurrent: [];
  selectCurrent: [];
  selectGroup: [key: string];
}>();

const permissionKeyword = defineModel<string>('permissionKeyword', {
  required: true,
});
const showSelectedOnly = defineModel<boolean>('showSelectedOnly', {
  required: true,
});
const selectedPermissions = defineModel<number[]>('selectedPermissions', {
  required: true,
});
</script>

<template>
  <section class="role-permission-panel">
    <div class="role-permission-shell">
      <aside class="role-permission-sidebar">
        <button
          v-for="item in permissionGroups"
          :key="item.key"
          :class="[{ 'is-active': activePermissionGroupKeyValue === item.key }]"
          :style="{ paddingLeft: `${10 + item.level * 16}px` }"
          class="role-module-item"
          type="button"
          @click="emit('selectGroup', item.key)"
        >
          <span class="role-module-name">{{ item.label }}</span>
          <span class="role-module-count">
            {{ item.selected }}/{{ item.total }}
          </span>
        </button>
      </aside>

      <section class="role-permission-main">
        <div class="role-permission-tools">
          <ElInput
            v-model="permissionKeyword"
            clearable
            placeholder="搜索权限名称或编码"
          />
          <div class="role-permission-tool-actions">
            <ElCheckbox v-model="showSelectedOnly">只看已选</ElCheckbox>
            <ElButton @click="emit('selectCurrent')">全选当前模块</ElButton>
            <ElButton @click="emit('clearCurrent')">清空当前模块</ElButton>
          </div>
        </div>

        <div class="role-permission-summary">
          <span>{{ activePermissionGroupLabel }}</span>
          <span>
            已选 {{ selectedPermissions.length }} / {{ permissionCount }}
          </span>
        </div>

        <ElCheckboxGroup v-model="selectedPermissions">
          <div
            v-if="filteredPermissions.length > 0"
            class="role-permission-list"
          >
            <ElCheckbox
              v-for="perm in filteredPermissions"
              :key="perm.id"
              :disabled="requiredPermissionIdSet.has(perm.id)"
              :label="perm.id"
              border
              class="role-permission-card"
            >
              <span class="role-permission-name">{{ perm.name }}</span>
              <span class="role-permission-action">
                {{
                  requiredPermissionIdSet.has(perm.id)
                    ? '菜单必需'
                    : actionLabel(perm.code)
                }}
              </span>
              <span class="role-permission-code">{{ perm.code }}</span>
            </ElCheckbox>
          </div>
          <ElEmpty v-else description="没有匹配的权限" />
        </ElCheckboxGroup>
      </section>
    </div>
  </section>
</template>

<style scoped src="./RoleAccessDialog.css"></style>
