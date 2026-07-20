<script lang="ts" setup>
import type { NotificationItem } from '@vben/layouts';

import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { AuthenticationLoginExpiredModal } from '@vben/common-ui';
import { useWatermark } from '@vben/hooks';
import { createIconifyIcon } from '@vben/icons';
import {
  BasicLayout,
  LockScreen,
  Notification,
  UserDropdown,
} from '@vben/layouts';
import { preferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';

import {
  clearNotificationsApi,
  getNotificationsApi,
  markAllReadApi,
  markReadApi,
  type NotificationDto,
} from '#/api/notification';
import { useAuthStore } from '#/store';
import { runHandled } from '#/utils/handled-promise';
import LoginForm from '#/views/_core/authentication/login.vue';

import { formatNotificationDate } from './notification-date';
import { resolveNotificationRoute } from './notification-route';
import Password from './password.vue';

const passwordRef = ref<InstanceType<typeof Password>>();
const passkeyIcon = createIconifyIcon('material-symbols:passkey-rounded');
const router = useRouter();
const userStore = useUserStore();
const authStore = useAuthStore();
const accessStore = useAccessStore();
// 通知数据（从后端获取）
const rawNotifications = ref<NotificationDto[]>([]);
const notifications = computed<NotificationItem[]>(() =>
  rawNotifications.value.map((n) => ({
    avatar: typeIcon(n.type),
    date: formatNotificationDate(n.createdAt),
    isRead: n.isRead,
    message: n.body,
    title: n.title,
    id: n.id,
    flowId: n.flowId,
    type: n.type,
  })),
);

function typeIcon(type: string): string {
  let emoji = '●';
  if (type === 'overdue') {
    emoji = '⚠';
  } else if (type.startsWith('due_soon')) {
    emoji = '⏰';
  } else if (type.includes('rejected')) {
    emoji = '✗';
  } else if (type.includes('approved')) {
    emoji = '✓';
  }
  return `data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><rect width="40" height="40" rx="20" fill="#e5e7eb"/><text x="50%" y="50%" dominant-baseline="central" text-anchor="middle" font-size="18">${emoji}</text></svg>`)}`;
}

async function loadNotifications() {
  try {
    rawNotifications.value = await getNotificationsApi();
  } catch {
    // 请求失败时清空旧通知，避免把过期数据继续展示为当前状态。
    rawNotifications.value = [];
  }
}

let pollTimer: null | ReturnType<typeof setInterval> = null;

onMounted(() => {
  runHandled(loadNotifications());
  pollTimer = setInterval(
    () => {
      runHandled(loadNotifications());
    },
    5 * 60 * 1000,
  ); // 5 分钟轮询
});

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer);
});

const { destroyWatermark, updateWatermark } = useWatermark();
const showDot = computed(() =>
  rawNotifications.value.some((item) => !item.isRead),
);

async function handlePasswordChanged() {
  await authStore.logout(false);
}

const menus = computed(() => [
  {
    handler: () => {
      passwordRef.value?.showPasswordPopup();
    },
    icon: passkeyIcon,
    text: '修改密码',
  },
]);

const avatar = computed(() => {
  return userStore.userInfo?.avatar ?? preferences.app.defaultAvatar;
});

async function handleLogout() {
  await authStore.logout(false);
}

async function handleNoticeClear() {
  try {
    await clearNotificationsApi();
    rawNotifications.value = [];
  } catch {
    // 静默失败
  }
}

async function handleMakeAll() {
  try {
    await markAllReadApi();
    rawNotifications.value = rawNotifications.value.map((n) => ({
      ...n,
      isRead: true,
    }));
  } catch {
    // 静默失败
  }
}

async function handleNoticeRead(item: NotificationItem) {
  if (item.id && !item.isRead) {
    try {
      await markReadApi(item.id as number);
      const target = rawNotifications.value.find((n) => n.id === item.id);
      if (target) target.isRead = true;
    } catch {
      // 已读状态失败不阻止用户进入业务页面
    }
  }
  const target = resolveNotificationRoute(item.type || '', item.flowId);
  const isMaterial = (item.type || '').startsWith('material_');
  if (
    target.path === '/approval/pending' &&
    !accessStore.accessCodes.includes('approval:handle')
  ) {
    await router.push(
      isMaterial && accessStore.accessCodes.includes('material-flow:approve')
        ? {
            path: '/material/approvals',
            query: { ...target.query, source: 'material' },
          }
        : '/home',
    );
    return;
  }
  if (
    target.path === '/approval/mine' &&
    !accessStore.accessCodes.includes('approval:view')
  ) {
    await router.push(
      isMaterial && accessStore.accessCodes.includes('material-flow:view')
        ? {
            path: '/material/applications',
            query: { ...target.query, source: 'material' },
          }
        : '/home',
    );
    return;
  }
  await router.push(target);
}

function handleNoticeViewAll() {
  if (accessStore.accessCodes.includes('approval:handle')) {
    runHandled(router.push('/approval/pending'));
  } else if (accessStore.accessCodes.includes('approval:view')) {
    runHandled(router.push('/approval/mine'));
  } else if (accessStore.accessCodes.includes('material-flow:view')) {
    runHandled(router.push('/material/applications?source=material'));
  } else {
    runHandled(router.push('/home'));
  }
}
watch(
  () => preferences.app.watermark,
  async (enable) => {
    if (enable) {
      await updateWatermark({
        content: `${userStore.userInfo?.username}`,
      });
    } else {
      destroyWatermark();
    }
  },
  {
    immediate: true,
  },
);
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
    <template #extra>
      <AuthenticationLoginExpiredModal
        v-model:open="accessStore.loginExpired"
        :avatar="avatar"
      >
        <LoginForm />
      </AuthenticationLoginExpiredModal>
    </template>
    <template #lock-screen>
      <LockScreen :avatar="avatar" @to-login="handleLogout" />
    </template>
  </BasicLayout>
  <Password ref="passwordRef" @changed="handlePasswordChanged" />
</template>
