<script lang="ts" setup>
import type { ProjectFormState } from './project-workspace-types';

import type { TestProjectOption } from '#/api/test-project';
import type { UserDto, UserOptionDto } from '#/api/user';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
} from 'element-plus';

defineProps<{
  editing: boolean;
  progressOptions: TestProjectOption[];
  projectTypeOptions: TestProjectOption[];
  saving: boolean;
  searchUsers?: (keyword: string) => Promise<void>;
  userOptionsLoading?: boolean;
  users: (UserDto | UserOptionDto)[];
}>();

const emit = defineEmits<{ save: [] }>();
const form = defineModel<ProjectFormState>('form', { required: true });
const visible = defineModel<boolean>('visible', { default: false });

function onProgressChange(value: string) {
  if (value !== 'closed') form.value.closedDate = '';
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="editing ? '编辑测试项目' : '新增测试项目'"
    width="720px"
  >
    <ElForm label-width="96px">
      <div class="form-grid">
        <ElFormItem label="项目编号" required>
          <ElInput v-model="form.code" placeholder="请输入项目编号" />
        </ElFormItem>
        <ElFormItem label="项目名称" required>
          <ElInput v-model="form.name" placeholder="请输入项目名称" />
        </ElFormItem>
        <ElFormItem label="项目类型" required>
          <ElSelect
            v-model="form.projectTypeCode"
            clearable
            placeholder="请选择"
          >
            <ElOption
              v-for="item in projectTypeOptions"
              :key="item.id"
              :label="item.label"
              :value="item.code"
            />
          </ElSelect>
        </ElFormItem>
        <ElFormItem label="进度" required>
          <ElSelect
            v-model="form.progressCode"
            clearable
            placeholder="请选择"
            @change="onProgressChange"
          >
            <ElOption
              v-for="item in progressOptions"
              :key="item.id"
              :label="item.label"
              :value="item.code"
            />
          </ElSelect>
        </ElFormItem>
        <ElFormItem label="负责人" required>
          <ElSelect
            v-model="form.ownerId"
            :loading="userOptionsLoading"
            :remote-method="searchUsers"
            clearable
            filterable
            placeholder="请选择"
            remote
          >
            <ElOption
              v-for="user in users"
              :key="user.id"
              :label="`${user.name}（${user.employeeNo}）`"
              :value="user.id"
            />
          </ElSelect>
        </ElFormItem>
        <ElFormItem label="跟进间隔" required>
          <ElInputNumber
            v-model="form.followUpIntervalDays"
            :max="365"
            :min="1"
            controls-position="right"
            style="width: 100%"
          />
        </ElFormItem>
        <ElFormItem label="开始时间" required>
          <ElDatePicker
            v-model="form.startDate"
            placeholder="选择日期"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </ElFormItem>
        <ElFormItem label="计划完成" required>
          <ElDatePicker
            v-model="form.plannedFinishDate"
            placeholder="选择日期"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </ElFormItem>
        <ElFormItem :required="form.progressCode === 'closed'" label="结案时间">
          <ElDatePicker
            v-model="form.closedDate"
            :disabled="form.progressCode !== 'closed'"
            placeholder="选择日期"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </ElFormItem>
      </div>
      <ElFormItem label="测试情况">
        <ElInput
          v-model="form.testStatus"
          :rows="4"
          maxlength="1000"
          placeholder="记录当前测试结论、阻塞点或风险"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="emit('save')">
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 16px;
}

@media (max-width: 768px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
