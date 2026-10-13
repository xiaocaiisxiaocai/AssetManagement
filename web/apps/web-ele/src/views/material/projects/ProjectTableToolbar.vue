<script lang="ts" setup>
import type { ProjectFilter } from './project-filter';
import type { DeleteStatus } from './project-workspace-types';

import type { TestProjectOption } from '#/api/test-project';

import { ElButton, ElInput, ElOption, ElSelect } from 'element-plus';

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
  ownerOptions: { id: number; name: string }[];
  progressOptions: TestProjectOption[];
  projectTypeOptions: TestProjectOption[];
  userOptionsLoading?: boolean;
}>();

const emit = defineEmits<{
  create: [];
  export: [];
  import: [];
  options: [];
  reset: [];
  search: [];
  statusChange: [];
  userSearch: [keyword: string];
}>();
const filter = defineModel<ProjectFilter>('filter', { required: true });
const deleteStatus = defineModel<DeleteStatus>('deleteStatus', {
  required: true,
});
</script>

<template>
  <div class="project-toolbar">
    <div class="project-toolbar-left">
      <ElInput
        v-model="filter.code"
        aria-label="项目编号"
        clearable
        placeholder="项目编号"
        style="width: 150px"
        @keyup.enter="emit('search')"
      />
      <ElInput
        v-model="filter.name"
        aria-label="项目名称"
        clearable
        placeholder="项目名称"
        style="width: 180px"
        @keyup.enter="emit('search')"
      />
      <ElSelect
        v-model="filter.projectTypeCode"
        aria-label="项目类型"
        clearable
        placeholder="项目类型"
        style="width: 130px"
      >
        <ElOption
          v-for="item in projectTypeOptions"
          :key="item.id"
          :label="item.label"
          :value="item.code"
        />
      </ElSelect>
      <ElSelect
        v-model="filter.ownerId"
        :loading="userOptionsLoading"
        :remote-method="(keyword: string) => emit('userSearch', keyword)"
        aria-label="负责人"
        clearable
        filterable
        placeholder="负责人"
        remote
        style="width: 150px"
      >
        <ElOption
          v-for="user in ownerOptions"
          :key="user.id"
          :label="user.name"
          :value="user.id"
        />
      </ElSelect>
      <ElSelect
        v-model="filter.progressCode"
        aria-label="项目进度"
        clearable
        placeholder="进度"
        style="width: 130px"
      >
        <ElOption
          v-for="item in progressOptions"
          :key="item.id"
          :label="item.label"
          :value="item.code"
        />
      </ElSelect>
      <ElSelect
        v-model="deleteStatus"
        aria-label="项目删除状态"
        placeholder="删除状态"
        style="width: 130px"
        @change="emit('statusChange')"
      >
        <ElOption label="全部" value="all" />
        <ElOption label="未删除" value="active" />
        <ElOption label="已删除" value="deleted" />
      </ElSelect>
      <ElButton type="primary" @click="emit('search')">查询</ElButton>
      <ElButton @click="emit('reset')">重置</ElButton>
    </div>
    <div class="project-toolbar-right">
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
