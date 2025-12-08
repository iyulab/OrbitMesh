import { Wifi, WifiOff, RefreshCw } from 'lucide-react'
import { useConnectionStore } from '@/stores/connection'
import { cn } from '@/lib/utils'

export function Header() {
  const { state, connect } = useConnectionStore()

  const statusConfig = {
    connected: {
      icon: Wifi,
      label: 'Connected',
      className: 'text-green-500',
    },
    connecting: {
      icon: RefreshCw,
      label: 'Connecting...',
      className: 'text-yellow-500 animate-spin',
    },
    reconnecting: {
      icon: RefreshCw,
      label: 'Reconnecting...',
      className: 'text-yellow-500 animate-spin',
    },
    disconnected: {
      icon: WifiOff,
      label: 'Disconnected',
      className: 'text-red-500',
    },
  }

  const config = statusConfig[state]
  const Icon = config.icon

  return (
    <header className="flex h-16 items-center justify-between border-b border-border bg-card px-6">
      <div>
        <h1 className="text-lg font-semibold text-foreground">Console</h1>
      </div>

      <div className="flex items-center gap-4">
        {/* Connection status */}
        <button
          onClick={() => state === 'disconnected' && connect()}
          className={cn(
            'flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm',
            state === 'disconnected' && 'cursor-pointer hover:bg-accent'
          )}
          disabled={state !== 'disconnected'}
        >
          <Icon className={cn('h-4 w-4', config.className)} />
          <span className="text-muted-foreground">{config.label}</span>
        </button>
      </div>
    </header>
  )
}
