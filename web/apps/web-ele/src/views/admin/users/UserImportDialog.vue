<script lang="ts" setup>
import type { UserImportRow } from '#/api/user';

import { ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineProps<{
  importing: boolean;
  rows: UserImportRow[];
  selectedFile: File | null;
}>();

const emit = defineEmits<{
  confirm: [];
  downloadTemplate: [];
  fileChange: [event: Event];
}>();
const visible = defineModel<boolean>('visible', { required: true });
const importFileInput = ref<HTMLInputElement | null>(null);

watch(visible, (opened) => {
  if (opened && importFileInput.value) {
    importFileInput.value.value = '';
  }
});

function chooseImportFile() {
  if (importFileInput.value) {
    importFileInput.value.value = '';
    importFileInput.value.click();
  }
}
</script>

<template>
  <ElDialog v-model="visible" title="批量导入用户" width="920px">
    <div class="user-import-toolbar">
      <ElButton @click="emit('downloadTemplate')">下载模板</ElButton>
      <input
        ref="importFileInput"
        accept=".xlsx"
        class="user-import-file-input"
        type="file"
        @change="emit('fileChange', $event)"
      />
      <ElButton @click="chooseImportFile">选择文件</ElButton>
      <span class="user-import-file-name">
        {{ selectedFile?.name || '未选择文件' }}
      </span>
      <ElButton
        :disabled="
          importing || rows.length === 0 || rows.some((row) => !row.isValid)
        "
        :loading="importing"
        type="primary"
        @click="emit('confirm')"
      >
        确认导入
      </ElButton>
    </div>
    <ElTable :data="rows" border max-height="360">
      <ElTableColumn label="行号" prop="row" width="80" />
      <ElTableColumn label="工号" min-width="120" prop="employeeNo" />
      <ElTableColumn label="姓名" min-width="120" prop="name" />
      <ElTableColumn label="邮箱" min-width="180" prop="email" />
      <ElTableColumn label="部门名称" min-width="140" prop="departmentName" />
      <ElTableColumn label="角色名称" min-width="140" prop="roleName" />
      <ElTableColumn label="状态" width="90">
        <template #default="{ row }">
          <ElTag :type="row.isValid ? 'success' : 'danger'">
            {{ row.isValid ? '有效' : '无效' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="错误" min-width="220" prop="error" />
    </ElTable>
  </ElDialog>
</template>

<style scoped src="./UserImportDialog.css"></style>
