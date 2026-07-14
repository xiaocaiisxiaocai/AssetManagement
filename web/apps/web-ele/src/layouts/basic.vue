<script lang="ts" setup>
import type { NotificationItem } from '@vben/layouts';
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { AuthenticationLoginExpiredModal } from '@vben/common-ui';
import { useWatermark } from '@vben/hooks';
import {
  BasicLayout,
  LockScreen,
  Notification,
  UserDropdown,
} from '@vben/layouts';
import { preferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';
import { createIconifyIcon } from '@vben/icons';
import { useAuthStore } from '#/store';
import {
  getNotificationsApi,
  markAllReadApi,
  markReadApi,
  type NotificationDto,
} from '#/api/notification';
import LoginForm from '#/views/_core/authentication/login.vue';
import Password from './password.vue';
import { resolveNotificationRoute } from './notification-route';

const passwordRef = ref<InstanceType<typeof Password>>();
const passkeyIcon = createIconifyIcon('material-symbols:passkey-rounded');
const router = useRouter();

// 通知数据（从后端获取）
const rawNotifications = ref<NotificationDto[]>([]);
const notifications = computed<NotificationItem[]>(() =>
  rawNotifications.value.map((n) => ({
    avatar: typeIcon(n.type),
    date: formatDate(n.createdAt),
    isRead: n.isRead,
    message: n.body,
    title: n.title,
    id: n.id,
    flowId: n.flowId,
    type: n.type,
  })),
);

function typeIcon(type: string): string {
  const emoji = type === 'overdue' ? '⚠' : type.startsWith('due_soon') ? '⏰' : type.includes('rejected') ? '✗' : type.includes('approved') ? '✓' : '●';
  return `data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><rect width="40" height="40" rx="20" fill="#e5e7eb"/><text x="50%" y="50%" dominant-baseline="central" text-anchor="middle" font-size="18">${emoji}</text></svg>`)}`;
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (diff < 60) return '刚刚';
  if (diff < 3600) return `${Math.floor(diff / 60)}分钟前`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}小时前`;
  return d.toLocaleDateString('zh-CN');
}

async function loadNotifications() {
  try {
    rawNotifications.value = await getNotificationsApi();
  } catch {
    // 静默失败，不影响页面
  }
}

let pollTimer: ReturnType<typeof setInterval> | null = null;

onMounted(() => {
  loadNotifications();
  pollTimer = setInterval(loadNotifications, 5 * 60 * 1000); // 5 分钟轮询
});

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer);
});

const userStore = useUserStore();
const authStore = useAuthStore();
const accessStore = useAccessStore();
const { destroyWatermark, updateWatermark } = useWatermark();
const showDot = computed(() =>
  rawNotifications.value.some((item) => !item.isRead),
);

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
    await markAllReadApi();
    rawNotifications.value = rawNotifications.value.map((n) => ({ ...n, isRead: true }));
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
  await router.push(resolveNotificationRoute(item.type || '', item.flowId));
}

function handleNoticeViewAll() {
  router.push('/approval/pending');
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
        :menus="menus"
        :text="userStore.userInfo?.realName"
        :description="userStore.userInfo?.username"
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
  <Password ref="passwordRef" />
</template>
