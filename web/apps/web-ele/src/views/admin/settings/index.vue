<script lang="ts" setup>
import type { SettingPayload, SystemSetting } from '#/api/base-data';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import { getSettingsApi, saveSettingsApi } from '#/api/base-data';
import {
  createPageSizeOptions,
  getDefaultPageSize,
  invalidateRuntimeSettings,
} from '#/utils/runtime-settings';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElMessage,
  ElOption,
  ElPagination,
  ElRadio,
  ElRadioGroup,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTimePicker,
} from 'element-plus';

defineOptions({ name: 'AdminSettings' });

const { hasAccessByCodes } = useAccess();
const canEditSettings = computed(() => hasAccessByCodes(['setting:edit']));
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingIndex = ref<null | number>(null);
const settings = ref<SystemSetting[]>([]);
const pageSizeOptions = ref(createPageSizeOptions(20));
const page = ref(1);
const pageSize = ref(20);
const assetConditionOptions = ref<string[]>([]);

const form = ref({
  key: '',
  value: '' as number | string | undefined,
  description: '',
});

const booleanSettingKeys = new Set([
  'audit_cleanup_enabled',
  'database_backup_enabled',
  'material.transfer.approval.enabled',
]);

const timeSettingKeys = new Set([
  'audit_cleanup_time',
  'database_backup_time',
]);

const integerSettingRules: Record<string, { max: number; min: number }> = {
  attachment_max_mb: { max: 100, min: 1 },
  audit_retention_months: { max: 120, min: 1 },
  database_backup_retention_days: { max: 3650, min: 1 },
  page_size: { max: 200, min: 1 },
};

const auditRetentionDayOptions = [7, 14, 30];

const pagedSettings = computed(() => {
  const start = (page.value - 1) * pageSize.value;
  return settings.value.slice(start, start + pageSize.value);
});

const formValueType = computed(() => getValueType(form.value.key));
const formNumberValue = computed<number | undefined>({
  get: () => (typeof form.value.value === 'number' ? form.value.value : undefined),
  set: (value) => {
    form.value.value = value;
  },
});

async function loadData() {
  loading.value = true;
  try {
    settings.value = await getSettingsApi();
    if ((page.value - 1) * pageSize.value >= settings.value.length) {
      page.value = 1;
    }
  } finally {
    loading.value = false;
  }
}

function openEdit(row: SystemSetting) {
  editingIndex.value = settings.value.findIndex((item) => item.key === row.key);
  form.value = {
    key: row.key,
    value: toFormValue(row.key, row.value),
    description: row.description ?? '',
  };
  assetConditionOptions.value =
    row.key === 'asset_condition_options' ? parseStringList(row.value) : [];
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

    if (editingIndex.value === null) {
      ElMessage.warning('请选择要编辑的系统参数');
      return;
    }

    updatedSettings[editingIndex.value] = {
      ...updatedSettings[editingIndex.value]!,
      value: toPayloadValue(form.value.key, form.value.value),
    };

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

function getValueType(key: string) {
  if (booleanSettingKeys.has(key)) return 'boolean';
  if (timeSettingKeys.has(key)) return 'time';
  if (key === 'audit_retention_days') return 'audit-retention-days';
  if (key === 'asset_condition_options') return 'asset-condition-options';
  if (integerSettingRules[key]) return 'integer';
  return 'text';
}

function toFormValue(key: string, value: string) {
  const valueType = getValueType(key);
  if (valueType === 'boolean') {
    return value.toLowerCase() === 'true' ? 'true' : 'false';
  }
  if (valueType === 'integer' || valueType === 'audit-retention-days') {
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) ? parsed : undefined;
  }
  return value;
}

function toPayloadValue(key: string, value: number | string | undefined): string {
  const valueType = getValueType(key);
  if (valueType === 'asset-condition-options') {
    const options = assetConditionOptions.value.map((item) => item.trim());
    if (
      options.length < 1 ||
      options.length > 20 ||
      options.some((item) => !item || item.length > 50) ||
      new Set(options.map((item) => item.toLowerCase())).size !== options.length
    ) {
      throw new Error('请配置 1-20 个不重复的状况选项，每项不超过 50 个字符');
    }
    return JSON.stringify(options);
  }
  if (valueType === 'boolean') {
    if (value !== 'true' && value !== 'false') {
      throw new Error('请选择参数值');
    }
    return value;
  }

  if (valueType === 'time') {
    const text = String(value ?? '').trim();
    if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(text)) {
      throw new Error('请选择正确的时间');
    }
    return text;
  }

  if (valueType === 'audit-retention-days') {
    const numberValue = Number(value);
    if (!auditRetentionDayOptions.includes(numberValue)) {
      throw new Error('请选择审计日志保留天数');
    }
    return String(numberValue);
  }

  if (valueType === 'integer') {
    const rule = integerSettingRules[key]!;
    const numberValue = Number(value);
    if (!Number.isInteger(numberValue) || numberValue < rule.min || numberValue > rule.max) {
      throw new Error(`参数值必须是 ${rule.min}-${rule.max} 的整数`);
    }
    return String(numberValue);
  }

  const text = String(value ?? '').trim();
  if (key === 'database_backup_path' && !text) {
    throw new Error('数据库备份目录不能为空');
  }
  if (key.startsWith('category_code_level') && key.endsWith('_length')) {
    const normalized = normalizeLengthRule(text);
    if (normalized === null) {
      throw new Error('编码段位数必须是 1-20 的整数或范围，例如 2 或 2-6');
    }
    return normalized;
  }
  if (key.startsWith('category_code_level') && key.endsWith('_regex')) {
    if (!text) {
      throw new Error('分类编码正则不能为空');
    }
    try {
      new RegExp(text);
    } catch {
      throw new Error('请输入合法正则表达式');
    }
  }
  return text;
}

function parseStringList(value: string) {
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed)
      ? parsed.filter((item): item is string => typeof item === 'string')
      : [];
  } catch {
    return [];
  }
}

function displaySettingValue(row: SystemSetting) {
  return row.key === 'asset_condition_options'
    ? parseStringList(row.value).join('、')
    : row.value;
}

function normalizeLengthRule(value: string) {
  const text = value.trim();
  if (/^\d+$/.test(text)) {
    const exact = Number(text);
    return exact >= 1 && exact <= 20 ? String(exact) : null;
  }
  const match = /^(\d+)\s*-\s*(\d+)$/.exec(text);
  if (!match) return null;
  const min = Number(match[1]);
  const max = Number(match[2]);
  if (!Number.isInteger(min) || !Number.isInteger(max) || min < 1 || max > 20 || min > max) {
    return null;
  }
  return `${min}-${max}`;
}

async function submitSave() {
  try {
    await save();
  } catch (error) {
    ElMessage.warning(error instanceof Error ? error.message : '参数值不合法');
  }
}

function onPageSizeChange() {
  page.value = 1;
}

onMounted(async () => {
  pageSize.value = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(pageSize.value);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="table-panel">
        <ElTable v-loading="loading" :data="pagedSettings" border height="100%">
          <ElTableColumn label="参数键" min-width="200" prop="key" />
          <ElTableColumn label="参数值" min-width="180">
            <template #default="{ row }">
              {{ displaySettingValue(row) }}
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="说明" min-width="260" prop="description" />
          <ElTableColumn v-if="canEditSettings" fixed="right" label="操作" width="160" align="center">
            <template #default="{ row }">
              <ElButton link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ settings.length }} 条</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect v-model="pageSize" style="width: 92px" @change="onPageSizeChange">
              <ElOption
                v-for="size in pageSizeOptions"
                :key="size"
                :label="`${size} 条`"
                :value="size"
              />
            </ElSelect>
          </div>
          <ElPagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="settings.length"
            background
            layout="prev, pager, next, jumper"
          />
        </div>
      </div>

      <ElDialog
        v-model="dialogVisible"
        title="编辑参数"
        width="540px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="参数键" required>
            <ElInput
              v-model="form.key"
              disabled
              placeholder="请输入参数键"
            />
          </ElFormItem>
          <ElFormItem label="参数值" required>
            <ElRadioGroup v-if="formValueType === 'boolean'" v-model="form.value">
              <ElRadio label="true">启用</ElRadio>
              <ElRadio label="false">禁用</ElRadio>
            </ElRadioGroup>
            <ElTimePicker
              v-else-if="formValueType === 'time'"
              v-model="form.value"
              format="HH:mm"
              placeholder="请选择时间"
              value-format="HH:mm"
            />
            <ElSelect
              v-else-if="formValueType === 'audit-retention-days'"
              v-model="form.value"
              placeholder="请选择保留天数"
              style="width: 100%"
            >
              <ElOption
                v-for="days in auditRetentionDayOptions"
                :key="days"
                :label="`${days} 天`"
                :value="days"
              />
            </ElSelect>
            <ElSelect
              v-else-if="formValueType === 'asset-condition-options'"
              v-model="assetConditionOptions"
              allow-create
              default-first-option
              filterable
              multiple
              placeholder="输入选项后按回车添加"
              style="width: 100%"
            />
            <ElInputNumber
              v-else-if="formValueType === 'integer'"
              v-model="formNumberValue"
              :max="integerSettingRules[form.key]?.max"
              :min="integerSettingRules[form.key]?.min"
              :step="1"
              :step-strictly="true"
              style="width: 100%"
            />
            <ElInput v-else v-model="form.value" placeholder="请输入参数值" />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton v-if="canEditSettings" :loading="saving" type="primary" @click="submitSave">保存</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
