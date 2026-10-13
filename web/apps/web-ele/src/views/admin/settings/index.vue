<script lang="ts" setup>
import SettingsFormDialog from './SettingsFormDialog.vue';
import SettingsTable from './SettingsTable.vue';
import { useSettingsPage } from './use-settings-page';

defineOptions({ name: 'AdminSettings' });

const {
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
} = useSettingsPage();
</script>

<template>
  <re-page>
    <div class="page-container">
      <SettingsTable
        v-model:page="page"
        v-model:page-size="pageSize"
        :can-edit="canEditSettings"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :paged-settings="pagedSettings"
        :total="settings.length"
        @edit="openEdit"
        @page-size-change="onPageSizeChange"
      />
      <SettingsFormDialog
        v-model:asset-condition-options="assetConditionOptions"
        v-model:form="form"
        v-model:visible="dialogVisible"
        :audit-retention-day-options="auditRetentionDayOptions"
        :can-edit="canEditSettings"
        :integer-setting-rules="integerSettingRules"
        :saving="saving"
        :value-type="formValueType"
        @save="submitSave"
      />
    </div>
  </re-page>
</template>
