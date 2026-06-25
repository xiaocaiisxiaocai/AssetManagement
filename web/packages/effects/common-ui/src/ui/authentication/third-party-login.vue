<script setup lang="ts">
import { MdiGithub, MdiGoogle, MdiQqchat, MdiWechat } from '@vben/icons';
import { $t } from '@vben/locales';
import { VbenIconButton } from '@vben-core/shadcn-ui';

defineOptions({
  name: 'ThirdPartyLogin',
});
</script>

<template>
  <div class="w-full sm:mx-auto md:max-w-md">
    <div class="mt-4 flex items-center justify-between">
      <span class="border-input w-[35%] border-b dark:border-gray-600"></span>
      <span class="text-muted-foreground text-center text-xs uppercase">
        {{ $t('authentication.thirdPartyLogin') }}
      </span>
      <span class="border-input w-[35%] border-b dark:border-gray-600"></span>
    </div>

    <div class="mt-4 flex flex-wrap justify-center">
      <VbenIconButton aria-label="微信登录" class="mb-3" title="微信登录">
        <MdiWechat />
      </VbenIconButton>
      <VbenIconButton aria-label="QQ 登录" class="mb-3" title="QQ 登录">
        <MdiQqchat />
      </VbenIconButton>
      <VbenIconButton aria-label="GitHub 登录" class="mb-3" title="GitHub 登录">
        <MdiGithub />
      </VbenIconButton>
      <VbenIconButton aria-label="Google 登录" class="mb-3" title="Google 登录">
        <MdiGoogle />
      </VbenIconButton>
    </div>
  </div>
</template>
