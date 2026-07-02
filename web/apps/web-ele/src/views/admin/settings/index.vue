<script lang="ts" setup>
import type { SettingPayload, SystemSetting } from '#/api/base-data';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import { getSettingsApi, saveSettingsApi } from '#/api/base-data';
import { invalidateRuntimeSettings } from '#/utils/runtime-settings';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElTable,
  ElTableColumn,
} from 'element-plus';

defineOptions({ name: 'AdminSettings' });

const { hasAccessByCodes } = useAccess();
const canEditSettings = computed(() => hasAccessByCodes(['setting:edit']));
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingIndex = ref<null | number>(null);
const settings = ref<SystemSetting[]>([]);

const form = ref({
  key: '',
  value: '',
  description: '',
});

async function loadData() {
  loading.value = true;
  try {
    settings.value = await getSettingsApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingIndex.value = null;
  form.value = { key: '', value: '', description: '' };
  dialogVisible.value = true;
}

function openEdit(row: SystemSetting, index: number) {
  editingIndex.value = index;
  form.value = { key: row.key, value: row.value, description: row.description ?? '' };
  dialogVisible.value = true;
}

async function save() {
  if (!form.value.key.trim()) {
    ElMessage.warning('请填写参数键');
    return;
  }

  saving.value = true;
  try {
    const updatedSettings = [...settings.value];

    if (editingIndex.value !== null) {
      updatedSettings[editingIndex.value] = {
        ...updatedSettings[editingIndex.value]!,
        key: form.value.key,
        value: form.value.value,
        description: form.value.description,
      };
    } else {
      updatedSettings.push({ id: 0, key: form.value.key, value: form.value.value, description: form.value.description });
    }

    const payload: SettingPayload[] = updatedSettings
      .filter((item) => item.key.trim())
      .map((item) => ({ description: item.description, key: item.key, value: item.value }));

    settings.value = await saveSettingsApi(payload);
    invalidateRuntimeSettings();
    ElMessage.success('保存成功');
    dialogVisible.value = false;
  } finally {
    saving.value = false;
  }
}

async function remove(index: number) {
  saving.value = true;
  try {
    const updatedSettings = settings.value.filter((_, i) => i !== index);
    const payload: SettingPayload[] = updatedSettings
      .filter((item) => item.key.trim())
      .map((item) => ({ description: item.description, key: item.key, value: item.value }));

    settings.value = await saveSettingsApi(payload);
    invalidateRuntimeSettings();
    ElMessage.success('删除成功');
  } finally {
    saving.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">系统参数配置</h2>
          <p class="page-subtitle">键值对配置管理</p>
        </div>
        <ElButton v-if="canEditSettings" type="primary" @click="openCreate">新增参数</ElButton>
      </div>

      <div class="table-panel">
        <ElTable v-loading="loading" :data="settings" border height="100%">
          <ElTableColumn label="参数键" min-width="200" prop="key" />
          <ElTableColumn label="参数值" min-width="180" prop="value" />
          <ElTableColumn class-name="hide-on-mobile" label="说明" min-width="260" prop="description" />
          <ElTableColumn v-if="canEditSettings" fixed="right" label="操作" width="160" align="center">
            <template #default="{ row, $index }">
              <ElButton link type="primary" size="small" @click="openEdit(row, $index)">编辑</ElButton>
              <ElButton link type="danger" size="small" @click="remove($index)">删除</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>

      <ElDialog
        v-model="dialogVisible"
        :title="editingIndex === null ? '新增参数' : '编辑参数'"
        width="540px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="参数键" required>
            <ElInput
              v-model="form.key"
              :disabled="editingIndex !== null"
              placeholder="请输入参数键"
            />
          </ElFormItem>
          <ElFormItem label="参数值" required>
            <ElInput v-model="form.value" placeholder="请输入参数值" />
          </ElFormItem>
          <ElFormItem label="说明">
            <ElInput
              v-model="form.description"
              :rows="3"
              clearable
              placeholder="请输入说明"
              type="textarea"
            />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton v-if="canEditSettings" :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
