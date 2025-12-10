import { createContext, useContext } from 'react'
import { useSignalR, type ConnectionStatus } from '@/hooks/useSignalR'
import type { HubConnection } from '@microsoft/signalr'

interface SignalRContextValue {
  connection: HubConnection | null
  connectionStatus: ConnectionStatus
  isConnected: boolean
  connect: () => Promise<HubConnection | undefined>
  disconnect: () => Promise<void>
}

const SignalRContext = createContext<SignalRContextValue | null>(null)

export function SignalRProvider({ children }: { children: React.ReactNode }) {
  const signalR = useSignalR()

  return (
    <SignalRContext.Provider value={signalR}>
      {children}
    </SignalRContext.Provider>
  )
}

export function useSignalRContext() {
  const context = useContext(SignalRContext)
  if (!context) {
    throw new Error('useSignalRContext must be used within a SignalRProvider')
  }
  return context
}
