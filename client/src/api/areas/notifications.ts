import { get, post } from '../client'
import type { NotificationItem } from '../types'

type Real = Record<string, unknown>
const toItem = (n: Real): NotificationItem => ({
  id: Number(n.id),
  type: (['order', 'payment', 'account', 'system'].includes(String(n.type).toLowerCase()) ? String(n.type).toLowerCase() : 'system') as NotificationItem['type'],
  title: String(n.title ?? n.subject ?? ''),
  body: String(n.body ?? n.message ?? ''),
  createdAt: String(n.createdAt ?? n.createdAtUtc ?? new Date().toISOString()),
  isRead: Boolean(n.isRead ?? n.read ?? n.readAtUtc ?? n.readAt),
  link: (n.link as string | null | undefined) ?? null,
})

export const notificationApi = {
  list: () => get<Real[] | { items: Real[] }>('notifications').then((v) => (Array.isArray(v) ? v : v.items).map(toItem)),
  unread: () => get<{ count: number }>('notifications/unread-count'),
  read: (id: number) => post<void>(`notifications/${id}/read`),
  readAll: () => post<void>('notifications/read-all'),
}
