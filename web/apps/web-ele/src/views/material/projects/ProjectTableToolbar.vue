<script lang="ts" setup>
import type { ProjectFilter } from './project-filter';

import { ElButton, ElInput } from 'element-plus';

type ProjectActionAccess = {
  canCreate: boolean;
  canDelete: boolean;
  canEdit: boolean;
  canExport: boolean;
  canImport: boolean;
  canOption: boolean;
  canPurge: boolean;
  canRestore: boolean;
};

defineProps<{
  access: ProjectActionAccess;
}>();

const emit = defineEmits<{
  create: [];
  export: [];
  import: [];
  options: [];
  reset: [];
  search: [];
}>();
const filter = defineModel<ProjectFilter>('filter', { required: true });
</script>

<template>
  <div class="project-toolbar">
    <div class="project-toolbar-left">
      <ElInput
        class="project-filter-field"
        v-model="filter.code"
        aria-label="项目编号"
        clearable
        placeholder="项目编号"
        style="width: 150px"
        @keyup.enter="emit('search')"
      />
      <ElInput
        class="project-filter-field"
        v-model="filter.name"
        aria-label="项目名称"
        clearable
        placeholder="项目名称"
        style="width: 180px"
        @keyup.enter="emit('search')"
      />
      <ElButton type="primary" @click="emit('search')">查询</ElButton>
      <ElButton @click="emit('reset')">重置</ElButton>
    </div>
    <div class="project-toolbar-right">
      <slot name="tools"></slot>
      <ElButton v-if="access.canOption" @click="emit('options')">配置</ElButton>
      <ElButton v-if="access.canImport" @click="emit('import')">
        批量导入
      </ElButton>
      <ElButton v-if="access.canExport" @click="emit('export')">
        导出 Excel
      </ElButton>
      <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
        新增项目
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./ProjectTable.css"></style>
