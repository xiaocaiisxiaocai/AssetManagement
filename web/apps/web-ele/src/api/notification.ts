import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export interface NotificationDto {
  id: number;
  type: string; // due_soon_1d | due_soon_3d | overdue
  title: string;
  body: string;
  flowId: number;
  isRead: boolean;
  createdAt: string;
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getNotificationsApi = (unreadOnly = false) =>
  unwrap(
    requestClient.get<ApiResult<NotificationDto[]>>('/notifications', {
      params: { unreadOnly },
    }),
  );

export const getUnreadCountApi = () =>
  unwrap(requestClient.get<ApiResult<number>>('/notifications/unread-count'));

export async function markReadApi(id: number): Promise<void> {
  await requestClient.post(`/notifications/${id}/read`);
}

export async function markAllReadApi(): Promise<void> {
  await requestClient.post('/notifications/read-all');
}
