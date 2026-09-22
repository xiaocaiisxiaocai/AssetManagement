<script lang="ts" setup>
import { Fallback } from '@vben/common-ui';
import { DEFAULT_HOME_PATH } from '@vben/constants';

defineOptions({ name: 'Fallback404Demo' });
</script>

<template>
  <Fallback :home-path="DEFAULT_HOME_PATH" status="404" />
</template>
