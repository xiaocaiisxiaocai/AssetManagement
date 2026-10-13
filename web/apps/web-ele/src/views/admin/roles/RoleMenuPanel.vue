<script lang="ts" setup>
import type { MenuDto } from '#/api/role';

import { ref } from 'vue';

import { ElTree } from 'element-plus';

defineProps<{
  pageMenuTree: MenuDto[];
  selectedMenuCount: number;
}>();

const menuTreeRef = ref<InstanceType<typeof ElTree>>();

function readSelection() {
  return {
    checkedKeys: (menuTreeRef.value?.getCheckedKeys(false) as number[]) ?? [],
    halfCheckedKeys:
      (menuTreeRef.value?.getHalfCheckedKeys() as number[]) ?? [],
  };
}

function setCheckedKeys(keys: number[]) {
  menuTreeRef.value?.setCheckedKeys(keys, false);
}

defineExpose({ readSelection, setCheckedKeys });
</script>

<template>
  <section class="role-menu-panel">
    <div class="role-access-section-title">
      <span>菜单范围</span>
      <span>{{ selectedMenuCount }} 项</span>
    </div>
    <div class="role-menu-tree">
      <ElTree
        ref="menuTreeRef"
        :data="pageMenuTree"
        :props="{ children: 'children', label: 'title' }"
        default-expand-all
        node-key="id"
        show-checkbox
      />
    </div>
  </section>
</template>

<style scoped src="./RoleAccessDialog.css"></style>
