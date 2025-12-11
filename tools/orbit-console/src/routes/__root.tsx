import { createRootRouteWithContext, Outlet } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/router-devtools'
import type { QueryClient } from '@tanstack/react-query'
import { Sidebar, Header } from '@/components/layout'
import { useConnectionStore } from '@/stores/connection'
import { useSignalRQueryInvalidation } from '@/hooks'
import { useEffect } from 'react'
import { Toaster } from 'sonner'

interface RouterContext {
  queryClient: QueryClient
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
})

function RootLayout() {
  const connect = useConnectionStore((s) => s.connect)

  // Auto-connect to SignalR on mount
  useEffect(() => {
    connect()
  }, [connect])

  // Enable real-time query invalidation
  useSignalRQueryInvalidation()

  return (
    <div className="flex h-screen bg-background">
      <Sidebar />
      <div className="flex flex-1 flex-col overflow-hidden">
        <Header />
        <main className="flex-1 overflow-auto p-6">
          <Outlet />
        </main>
      </div>
      <Toaster position="top-right" richColors closeButton />
      {import.meta.env.DEV && <TanStackRouterDevtools position="bottom-right" />}
    </div>
  )
}
