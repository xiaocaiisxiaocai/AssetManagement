<script setup lang="ts">
import { $t } from '@vben/locales';

import SwitchItem from '../switch-item.vue';

defineOptions({
  name: 'PreferenceGeneralConfig',
});

const appDynamicTitle = defineModel<boolean>('appDynamicTitle');
const appWatermark = defineModel<boolean>('appWatermark');
const appEnableCheckUpdates = defineModel<boolean>('appEnableCheckUpdates');
</script>

<template>
  <SwitchItem v-model="appDynamicTitle">
    {{ $t('preferences.dynamicTitle') }}
  </SwitchItem>
  <SwitchItem v-model="appWatermark">
    {{ $t('preferences.watermark') }}
  </SwitchItem>
  <SwitchItem v-model="appEnableCheckUpdates">
    {{ $t('preferences.checkUpdates') }}
  </SwitchItem>
</template>
