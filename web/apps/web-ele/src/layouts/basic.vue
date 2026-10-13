<script lang="ts" setup>
import { defineAsyncComponent } from 'vue';

import {
  BasicLayout,
  LockScreen,
  Notification,
  UserDropdown,
} from '@vben/layouts';

import { useBasicLayout } from './use-basic-layout';

const Password = defineAsyncComponent(() => import('./password.vue'));

const {
  avatar,
  handleLogout,
  handleMakeAll,
  handleNoticeClear,
  handleNoticeRead,
  handleNoticeViewAll,
  handlePasswordChanged,
  menus,
  notifications,
  passwordVisible,
  showDot,
  userStore,
} = useBasicLayout();
</script>

<template>
  <BasicLayout @clear-preferences-and-logout="handleLogout">
    <template #user-dropdown>
      <UserDropdown
        :avatar="avatar"
        :description="userStore.userInfo?.username"
        :menus="menus"
        :text="userStore.userInfo?.realName"
        @logout="handleLogout"
      />
    </template>
    <template #notification>
      <Notification
        :dot="showDot"
        :notifications="notifications"
        @clear="handleNoticeClear"
        @make-all="handleMakeAll"
        @read="handleNoticeRead"
        @view-all="handleNoticeViewAll"
      />
    </template>
    <template #lock-screen>
      <LockScreen :avatar="avatar" @to-login="handleLogout" />
    </template>
  </BasicLayout>
  <Password
    v-if="passwordVisible"
    v-model:open="passwordVisible"
    @changed="handlePasswordChanged"
  />
</template>
