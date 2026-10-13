import type { SettingFormState } from './settings-form';

import type { SettingPayload, SystemSetting } from '#/api/base-data';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import { ElMessage } from 'element-plus';

import { getSettingsApi, saveSettingsApi } from '#/api/base-data';
import {
  createPageSizeOptions,
  getDefaultPageSize,
  invalidateRuntimeSettings,
} from '#/utils/runtime-settings';

import {
  auditRetentionDayOptions,
  getValueType,
  integerSettingRules,
  parseStringList,
  toFormValue,
  toPayloadValue,
} from './settings-value';

export function useSettingsPage() {
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
  const form = ref<SettingFormState>({
    description: '',
    key: '',
    value: '',
  });

  const pagedSettings = computed(() => {
    const start = (page.value - 1) * pageSize.value;
    return settings.value.slice(start, start + pageSize.value);
  });
  const formValueType = computed(() => getValueType(form.value.key));

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
    editingIndex.value = settings.value.findIndex(
      (item) => item.key === row.key,
    );
    form.value = {
      description: row.description ?? '',
      key: row.key,
      value: toFormValue(row.key, row.value),
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

      const current = updatedSettings[editingIndex.value];
      if (!current) {
        throw new Error('请选择要编辑的系统参数');
      }
      updatedSettings[editingIndex.value] = {
        ...current,
        value: toPayloadValue(
          form.value.key,
          form.value.value,
          assetConditionOptions.value,
        ),
      };

      const payload: SettingPayload[] = updatedSettings
        .filter((item) => item.key.trim())
        .map((item) => ({
          description: item.description,
          key: item.key,
          value: item.value,
        }));

      settings.value = await saveSettingsApi(payload);
      invalidateRuntimeSettings();
      ElMessage.success('保存成功');
      dialogVisible.value = false;
    } finally {
      saving.value = false;
    }
  }

  async function submitSave() {
    try {
      await save();
    } catch (error) {
      ElMessage.warning(
        error instanceof Error ? error.message : '参数值不合法',
      );
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

  return {
    assetConditionOptions,
    auditRetentionDayOptions,
    canEditSettings,
    dialogVisible,
    form,
    formValueType,
    integerSettingRules,
    loading,
    onPageSizeChange,
    openEdit,
    page,
    pageSize,
    pageSizeOptions,
    pagedSettings,
    saving,
    settings,
    submitSave,
  };
}
