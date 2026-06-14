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
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">系统参数</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            管理审计保留、附件大小、默认分页等基础参数。
          </p>
        </div>
        <div class="flex gap-2">
          <ElButton @click="addRow">新增参数</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </div>
      </div>

      <ElForm>
        <ElTable v-loading="loading" :data="settings" border>
          <ElTableColumn label="参数键" min-width="180">
            <template #default="{ row }">
              <ElInput v-model="row.key" />
            </template>
          </ElTableColumn>
          <ElTableColumn label="参数值" min-width="160">
            <template #default="{ row }">
              <ElInput v-model="row.value" />
            </template>
          </ElTableColumn>
          <ElTableColumn label="说明" min-width="240">
            <template #default="{ row }">
              <ElInput v-model="row.description" />
            </template>
          </ElTableColumn>
        </ElTable>
        <ElFormItem class="mt-4">
          <ElButton :loading="saving" type="primary" @click="save">保存参数</ElButton>
        </ElFormItem>
      </ElForm>
    </div>
  </re-page>
</template>
