<script lang="ts" setup>
import { Fullscreen, Minimize2 } from '@vben-core/icons';

const screen = defineModel<boolean>('screen');

function toggleScreen() {
  screen.value = !screen.value;
}
</script>

<template>
  <button
    :aria-label="screen ? '退出标签页全屏' : '标签页全屏'"
    class="flex-center hover:bg-muted hover:text-foreground text-muted-foreground border-border h-full cursor-pointer border-l px-2 text-lg font-semibold"
    :title="screen ? '退出标签页全屏' : '标签页全屏'"
    type="button"
    @click="toggleScreen"
  >
    <Minimize2 v-if="screen" class="size-4" />
    <Fullscreen v-else class="size-4" />
  </button>
</template>
