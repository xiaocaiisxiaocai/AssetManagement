import type { NotificationItem } from '@vben/layouts';

import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { useWatermark } from '@vben/hooks';
import { createIconifyIcon } from '@vben/icons';
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

import { formatNotificationDate } from './notification-date';
import { resolveNotificationRoute } from './notification-route';
import { createNotificationSyncGuard } from './notification-sync-guard';

export function useBasicLayout() {
  const passwordVisible = ref(false);
  const passkeyIcon = createIconifyIcon('material-symbols:passkey-rounded');
  const router = useRouter();
  const userStore = useUserStore();
  const authStore = useAuthStore();
  const accessStore = useAccessStore();
  const notificationSyncGuard = createNotificationSyncGuard();
  const rawNotifications = ref<NotificationDto[]>([]);
  const notifications = computed<NotificationItem[]>(() =>
    rawNotifications.value.map((item) => ({
      avatar: typeIcon(item.type),
      date: formatNotificationDate(item.createdAt),
      isRead: item.isRead,
      message: item.body,
      title: item.title,
      id: item.id,
      flowId: item.flowId,
      type: item.type,
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
    const generation = notificationSyncGuard.beginRefresh();
    if (generation === null) return;

    try {
      const nextNotifications = await getNotificationsApi();
      if (notificationSyncGuard.canCommitRefresh(generation)) {
        rawNotifications.value = nextNotifications;
      }
    } catch {
      // 轮询失败保留上次成功结果，下一轮继续尝试。
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
    );
  });

  onUnmounted(() => {
    if (pollTimer) clearInterval(pollTimer);
    notificationSyncGuard.invalidate();
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
        passwordVisible.value = true;
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
      await notificationSyncGuard.enqueueMutation(async () => {
        await clearNotificationsApi();
        rawNotifications.value = [];
      });
    } catch {
      // 静默失败
    }
  }

  async function handleMakeAll() {
    try {
      await notificationSyncGuard.enqueueMutation(async () => {
        await markAllReadApi();
        rawNotifications.value = rawNotifications.value.map((item) => ({
          ...item,
          isRead: true,
        }));
      });
    } catch {
      // 静默失败
    }
  }

  async function handleNoticeRead(item: NotificationItem) {
    if (item.id && !item.isRead) {
      try {
        await notificationSyncGuard.enqueueMutation(async () => {
          await markReadApi(item.id as number);
          const target = rawNotifications.value.find(
            (notification) => notification.id === item.id,
          );
          if (target) target.isRead = true;
        });
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

  return {
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
  };
}
