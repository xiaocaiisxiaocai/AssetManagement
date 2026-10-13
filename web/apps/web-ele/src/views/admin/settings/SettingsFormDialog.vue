<script lang="ts" setup>
import type { SettingFormState, SettingValueType } from './settings-form';

import { computed } from 'vue';

import { ElButton, ElDialog, ElForm, ElFormItem, ElInput } from 'element-plus';

import SettingsValueAuditRetention from './SettingsValueAuditRetention.vue';
import SettingsValueBoolean from './SettingsValueBoolean.vue';
import SettingsValueConditionOptions from './SettingsValueConditionOptions.vue';
import SettingsValueInteger from './SettingsValueInteger.vue';
import SettingsValueText from './SettingsValueText.vue';
import SettingsValueTime from './SettingsValueTime.vue';

defineProps<{
  auditRetentionDayOptions: Set<number>;
  canEdit: boolean;
  integerSettingRules: Record<string, { max: number; min: number }>;
  saving: boolean;
  valueType: SettingValueType;
}>();

const emit = defineEmits<{
  save: [];
}>();
const form = defineModel<SettingFormState>('form', { required: true });
const visible = defineModel<boolean>('visible', { required: true });
const assetConditionOptions = defineModel<string[]>('assetConditionOptions', {
  required: true,
});
const formNumberValue = computed<number | undefined>({
  get: () =>
    typeof form.value.value === 'number' ? form.value.value : undefined,
  set: (value) => {
    form.value.value = value;
  },
});
</script>

<template>
  <ElDialog v-model="visible" title="编辑参数" width="540px">
    <ElForm label-width="100px">
      <ElFormItem label="参数键" required>
        <ElInput v-model="form.key" disabled placeholder="请输入参数键" />
      </ElFormItem>
      <ElFormItem label="参数值" required>
        <SettingsValueBoolean
          v-if="valueType === 'boolean'"
          v-model:value="form.value"
        />
        <SettingsValueTime
          v-else-if="valueType === 'time'"
          v-model:value="form.value"
        />
        <SettingsValueAuditRetention
          v-else-if="valueType === 'audit-retention-days'"
          v-model:value="form.value"
          :options="auditRetentionDayOptions"
        />
        <SettingsValueConditionOptions
          v-else-if="valueType === 'asset-condition-options'"
          v-model:options="assetConditionOptions"
        />
        <SettingsValueInteger
          v-else-if="valueType === 'integer'"
          v-model:value="formNumberValue"
          :max="integerSettingRules[form.key]?.max"
          :min="integerSettingRules[form.key]?.min"
        />
        <SettingsValueText v-else v-model:value="form.value" />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton
        v-if="canEdit"
        :loading="saving"
        type="primary"
        @click="emit('save')"
      >
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>
