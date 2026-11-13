import { get, post } from '../client'
import type { NotificationItem } from '../types'

export const notificationApi = {
  list: () => get<NotificationItem[]>('notifications'),
  unread: () => get<{ count: number }>('notifications/unread-count'),
  read: (id: number) => post<void>(`notifications/${id}/read`),
  /** TODO(BE): mark-all-read does not exist yet. */
  readAll: () => post<void>('notifications/read-all'),
}
