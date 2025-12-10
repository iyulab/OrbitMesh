import { useEffect, useRef, useCallback, useState } from 'react'
import { HubConnectionBuilder, HubConnection, LogLevel, HubConnectionState } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from '@/components/ui/sonner'
import { useNotificationStore } from '@/stores/notificationStore'

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

export function useSignalR() {
  const connectionRef = useRef<HubConnection | null>(null)
  const queryClient = useQueryClient()
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected')
  const addNotification = useNotificationStore((state) => state.addNotification)

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === HubConnectionState.Connected) {
      return connectionRef.current
    }

    setConnectionStatus('connecting')

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/dashboard')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build()

    // Handle agent updates
    connection.on('AgentConnected', (agentId: string) => {
      console.log('Agent connected:', agentId)
      queryClient.invalidateQueries({ queryKey: ['agents'] })
      queryClient.invalidateQueries({ queryKey: ['agent', agentId] })
      const message = `Agent ${agentId.substring(0, 8)}... is now online`
      toast.success('Agent connected', { description: message })
      addNotification({
        type: 'success',
        title: 'Agent connected',
        message,
        actionUrl: `/agents`,
        actionLabel: 'View agents',
      })
    })

    connection.on('AgentDisconnected', (agentId: string) => {
      console.log('Agent disconnected:', agentId)
      queryClient.invalidateQueries({ queryKey: ['agents'] })
      queryClient.invalidateQueries({ queryKey: ['agent', agentId] })
      const message = `Agent ${agentId.substring(0, 8)}... went offline`
      toast.warning('Agent disconnected', { description: message })
      addNotification({
        type: 'warning',
        title: 'Agent disconnected',
        message,
        actionUrl: `/agents`,
        actionLabel: 'View agents',
      })
    })

    connection.on('AgentStatusChanged', (agentId: string, status: string) => {
      console.log('Agent status changed:', agentId, status)
      queryClient.invalidateQueries({ queryKey: ['agents'] })
      queryClient.invalidateQueries({ queryKey: ['agent', agentId] })
    })

    // Handle job updates
    connection.on('JobCreated', (jobId: string) => {
      console.log('Job created:', jobId)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    })

    connection.on('JobStatusChanged', (jobId: string, status: string) => {
      console.log('Job status changed:', jobId, status)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
    })

    connection.on('JobProgress', (jobId: string, progress: number, message: string) => {
      console.log('Job progress:', jobId, progress, message)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
    })

    connection.on('JobCompleted', (jobId: string) => {
      console.log('Job completed:', jobId)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
      const message = `Job ${jobId.substring(0, 8)}... finished successfully`
      toast.success('Job completed', { description: message })
      addNotification({
        type: 'success',
        title: 'Job completed',
        message,
        actionUrl: `/jobs/${jobId}`,
        actionLabel: 'View job',
      })
    })

    connection.on('JobFailed', (jobId: string, error: string) => {
      console.log('Job failed:', jobId, error)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
      const message = error || `Job ${jobId.substring(0, 8)}... failed`
      toast.error('Job failed', { description: message })
      addNotification({
        type: 'error',
        title: 'Job failed',
        message,
        actionUrl: `/jobs/${jobId}`,
        actionLabel: 'View job',
      })
    })

    // Handle workflow updates
    connection.on('WorkflowInstanceStarted', (instanceId: string, workflowId: string) => {
      console.log('Workflow instance started:', instanceId, workflowId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
    })

    connection.on('WorkflowInstanceCompleted', (instanceId: string) => {
      console.log('Workflow instance completed:', instanceId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
      const message = `Workflow instance ${instanceId.substring(0, 8)}... finished`
      toast.success('Workflow completed', { description: message })
      addNotification({
        type: 'success',
        title: 'Workflow completed',
        message,
        actionUrl: `/workflows`,
        actionLabel: 'View workflows',
      })
    })

    connection.on('WorkflowInstanceFailed', (instanceId: string, error: string) => {
      console.log('Workflow instance failed:', instanceId, error)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
      const message = error || `Workflow instance ${instanceId.substring(0, 8)}... failed`
      toast.error('Workflow failed', { description: message })
      addNotification({
        type: 'error',
        title: 'Workflow failed',
        message,
        actionUrl: `/workflows`,
        actionLabel: 'View workflows',
      })
    })

    connection.on('WorkflowStepStarted', (instanceId: string, stepId: string) => {
      console.log('Workflow step started:', instanceId, stepId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    connection.on('WorkflowStepCompleted', (instanceId: string, stepId: string) => {
      console.log('Workflow step completed:', instanceId, stepId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    // Handle reconnection events
    connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting...', error)
      setConnectionStatus('reconnecting')
      toast.warning('Reconnecting...', {
        description: 'Connection to server lost, attempting to reconnect',
      })
    })

    connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId)
      setConnectionStatus('connected')
      toast.success('Reconnected', {
        description: 'Connection to server restored',
      })
      // Refresh all queries on reconnect
      queryClient.invalidateQueries({ queryKey: ['agents'] })
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    connection.onclose((error) => {
      console.warn('SignalR connection closed:', error)
      setConnectionStatus('disconnected')
    })

    try {
      await connection.start()
      console.log('SignalR connected')
      connectionRef.current = connection
      setConnectionStatus('connected')
      return connection
    } catch (error) {
      console.error('SignalR connection failed:', error)
      setConnectionStatus('disconnected')
      throw error
    }
  }, [queryClient, addNotification])

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      await connectionRef.current.stop()
      connectionRef.current = null
      setConnectionStatus('disconnected')
    }
  }, [])

  useEffect(() => {
    connect().catch(console.error)

    return () => {
      disconnect().catch(console.error)
    }
  }, [connect, disconnect])

  return {
    connection: connectionRef.current,
    connectionStatus,
    isConnected: connectionStatus === 'connected',
    connect,
    disconnect,
  }
}
