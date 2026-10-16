<script lang="ts" setup>
import type { UserListQuery } from './user-form';

import { ElButton, ElForm, ElFormItem, ElInput } from 'element-plus';

const emit = defineEmits<{
  reset: [];
  search: [];
}>();
const query = defineModel<UserListQuery>('query', { required: true });
</script>

<template>
  <div class="filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="搜索">
        <ElInput
          v-model="query.keyword"
          clearable
          placeholder="工号或姓名"
          style="width: 240px"
          @keyup.enter="emit('search')"
        />
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>

<style scoped src="./UserFilter.css"></style>
