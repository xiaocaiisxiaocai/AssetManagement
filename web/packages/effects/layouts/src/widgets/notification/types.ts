interface NotificationItem {
  avatar: string;
  date: string;
  id?: number;
  isRead?: boolean;
  message: string;
  title: string;
  flowId?: number;
  type?: string;
}

export type { NotificationItem };
