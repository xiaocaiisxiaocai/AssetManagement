import { requestClient } from '#/api/request';

export interface NotificationDto {
  id: number;
  type: string; // due_soon_1d | due_soon_3d | overdue
  title: string;
  body: string;
  flowId: number;
  isRead: boolean;
  createdAt: string;
}

export async function getNotificationsApi(unreadOnly = false): Promise<NotificationDto[]> {
  const res = await requestClient.get<{ code: number; data: NotificationDto[] }>(
    '/notifications',
    { params: { unreadOnly } },
  );
  return res.data ?? [];
}

export async function getUnreadCountApi(): Promise<number> {
  const res = await requestClient.get<{ code: number; data: number }>(
    '/notifications/unread-count',
  );
  return res.data ?? 0;
}

export async function markReadApi(id: number): Promise<void> {
  await requestClient.post(`/notifications/${id}/read`);
}

export async function markAllReadApi(): Promise<void> {
  await requestClient.post('/notifications/read-all');
}
