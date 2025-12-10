import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export type NotificationType = 'info' | 'success' | 'warning' | 'error'

export interface Notification {
  id: string
  type: NotificationType
  title: string
  message?: string
  timestamp: number
  read: boolean
  actionUrl?: string
  actionLabel?: string
}

interface NotificationState {
  notifications: Notification[]
  unreadCount: number
  maxNotifications: number

  // Actions
  addNotification: (notification: Omit<Notification, 'id' | 'timestamp' | 'read'>) => void
  markAsRead: (id: string) => void
  markAllAsRead: () => void
  removeNotification: (id: string) => void
  clearAll: () => void
  clearRead: () => void
}

export const useNotificationStore = create<NotificationState>()(
  persist(
    (set) => ({
      notifications: [],
      unreadCount: 0,
      maxNotifications: 100,

      addNotification: (notification) => {
        const newNotification: Notification = {
          ...notification,
          id: `notification-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
          timestamp: Date.now(),
          read: false,
        }

        set((state) => {
          // Add new notification at the beginning
          const notifications = [newNotification, ...state.notifications]

          // Keep only maxNotifications
          const trimmedNotifications = notifications.slice(0, state.maxNotifications)

          return {
            notifications: trimmedNotifications,
            unreadCount: trimmedNotifications.filter((n) => !n.read).length,
          }
        })
      },

      markAsRead: (id) => {
        set((state) => {
          const notifications = state.notifications.map((n) =>
            n.id === id ? { ...n, read: true } : n
          )
          return {
            notifications,
            unreadCount: notifications.filter((n) => !n.read).length,
          }
        })
      },

      markAllAsRead: () => {
        set((state) => ({
          notifications: state.notifications.map((n) => ({ ...n, read: true })),
          unreadCount: 0,
        }))
      },

      removeNotification: (id) => {
        set((state) => {
          const notifications = state.notifications.filter((n) => n.id !== id)
          return {
            notifications,
            unreadCount: notifications.filter((n) => !n.read).length,
          }
        })
      },

      clearAll: () => {
        set({ notifications: [], unreadCount: 0 })
      },

      clearRead: () => {
        set((state) => {
          const notifications = state.notifications.filter((n) => !n.read)
          return {
            notifications,
            unreadCount: notifications.length,
          }
        })
      },
    }),
    {
      name: 'orbit-notifications',
      partialize: (state) => ({
        notifications: state.notifications.slice(0, 50), // Persist only 50 notifications
      }),
    }
  )
)

// Helper function to add notification with toast
export function notify(
  type: NotificationType,
  title: string,
  message?: string,
  options?: {
    actionUrl?: string
    actionLabel?: string
  }
) {
  useNotificationStore.getState().addNotification({
    type,
    title,
    message,
    actionUrl: options?.actionUrl,
    actionLabel: options?.actionLabel,
  })
}
