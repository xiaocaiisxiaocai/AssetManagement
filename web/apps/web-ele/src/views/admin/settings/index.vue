<script lang="ts" setup>
import type { SettingPayload, SystemSetting } from '#/api/base-data';

import { onMounted, ref } from 'vue';

import { getSettingsApi, saveSettingsApi } from '#/api/base-data';

import {
  ElButton,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElTable,
  ElTableColumn,
} from 'element-plus';

defineOptions({ name: 'AdminSettings' });

const loading = ref(false);
const saving = ref(false);
const settings = ref<SystemSetting[]>([]);

async function loadData() {
  loading.value = true;
  try {
    settings.value = await getSettingsApi();
  } finally {
    loading.value = false;
  }
}

function addRow() {
  settings.value.push({
    description: '',
    id: 0,
    key: '',
    value: '',
  });
}

async function save() {
  const payload: SettingPayload[] = settings.value
    .filter((item) => item.key.trim())
    .map((item) => ({
      description: item.description,
      key: item.key,
      value: item.value,
    }));
  saving.value = true;
  try {
    settings.value = await saveSettingsApi(payload);
    ElMessage.success('保存成功');
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
        <div class="page-actions">
          <ElButton @click="addRow">新增参数</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </div>
      </div>

      <div class="table-panel">
        <ElForm>
          <ElTable v-loading="loading" :data="settings" border>
            <ElTableColumn label="参数键" min-width="200">
              <template #default="{ row }">
                <ElInput v-model="row.key" placeholder="请输入参数键" />
              </template>
            </ElTableColumn>
            <ElTableColumn label="参数值" min-width="180">
              <template #default="{ row }">
                <ElInput v-model="row.value" placeholder="请输入参数值" />
              </template>
            </ElTableColumn>
            <ElTableColumn class-name="hide-on-mobile" label="说明" min-width="260">
              <template #default="{ row }">
                <ElInput v-model="row.description" placeholder="请输入说明" />
              </template>
            </ElTableColumn>
          </ElTable>
          <ElFormItem style="margin-top: 20px;">
            <ElButton :loading="saving" type="primary" @click="save">保存全部参数</ElButton>
          </ElFormItem>
        </ElForm>
      </div>
    </div>
  </re-page>
</template>
