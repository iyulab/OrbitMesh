import { useEffect, useRef, useCallback, useState } from 'react'
import { HubConnectionBuilder, HubConnection, LogLevel, HubConnectionState } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from '@/components/ui/sonner'
import { useNotificationStore } from '@/stores/notificationStore'
import type { Agent, Job, AgentStatus, JobStatus, WorkflowInstance, WorkflowInstanceStatus } from '@/types'

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

// Helper to update a single item in a list cache
function updateItemInList<T extends { id: string }>(
  list: T[] | undefined,
  id: string,
  updater: (item: T) => T
): T[] | undefined {
  if (!list) return undefined
  return list.map((item) => (item.id === id ? updater(item) : item))
}

export function useSignalR() {
  const connectionRef = useRef<HubConnection | null>(null)
  const isConnectingRef = useRef(false)
  const queryClient = useQueryClient()
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected')
  const addNotification = useNotificationStore((state) => state.addNotification)

  const connect = useCallback(async () => {
    // Prevent duplicate connections (especially in StrictMode)
    if (
      connectionRef.current?.state === HubConnectionState.Connected ||
      connectionRef.current?.state === HubConnectionState.Connecting ||
      isConnectingRef.current
    ) {
      return connectionRef.current ?? undefined
    }

    isConnectingRef.current = true
    setConnectionStatus('connecting')

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/dashboard')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build()

    // Handle agent updates
    connection.on('AgentConnected', (agentId: string) => {
      console.log('Agent connected:', agentId)
      // New agent - need full data, so invalidate
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
      // Update agent status in cache directly
      queryClient.setQueryData<Agent[]>(['agents'], (old) =>
        updateItemInList(old, agentId, (agent) => ({
          ...agent,
          status: 'Disconnected' as AgentStatus,
        }))
      )
      queryClient.setQueryData<Agent>(['agent', agentId], (old) =>
        old ? { ...old, status: 'Disconnected' as AgentStatus } : old
      )
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
      // Update agent status in cache directly
      queryClient.setQueryData<Agent[]>(['agents'], (old) =>
        updateItemInList(old, agentId, (agent) => ({
          ...agent,
          status: status as AgentStatus,
        }))
      )
      queryClient.setQueryData<Agent>(['agent', agentId], (old) =>
        old ? { ...old, status: status as AgentStatus } : old
      )
    })

    // Handle job updates
    connection.on('JobCreated', (jobId: string) => {
      console.log('Job created:', jobId)
      // New job - need full data, so invalidate
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    })

    connection.on('JobStatusChanged', (jobId: string, status: string) => {
      console.log('Job status changed:', jobId, status)
      // Update job status in cache directly
      queryClient.setQueriesData<Job[]>({ queryKey: ['jobs'] }, (old) =>
        updateItemInList(old, jobId, (job) => ({
          ...job,
          status: status as JobStatus,
        }))
      )
      queryClient.setQueryData<Job>(['job', jobId], (old) =>
        old ? { ...old, status: status as JobStatus } : old
      )
    })

    connection.on('JobProgress', (jobId: string, percentage: number, message: string) => {
      console.log('Job progress:', jobId, percentage, message)
      // Update job progress in cache directly - NO invalidation!
      const progressUpdate = { jobId, percentage, message }
      queryClient.setQueriesData<Job[]>({ queryKey: ['jobs'] }, (old) =>
        updateItemInList(old, jobId, (job) => ({
          ...job,
          progress: progressUpdate,
        }))
      )
      queryClient.setQueryData<Job>(['job', jobId], (old) =>
        old ? { ...old, progress: progressUpdate } : old
      )
    })

    connection.on('JobCompleted', (jobId: string) => {
      console.log('Job completed:', jobId)
      const now = new Date().toISOString()
      // Update job status in cache directly
      queryClient.setQueriesData<Job[]>({ queryKey: ['jobs'] }, (old) =>
        updateItemInList(old, jobId, (job) => ({
          ...job,
          status: 'Completed' as JobStatus,
          completedAt: now,
          progress: job.progress ? { ...job.progress, percentage: 100 } : undefined,
        }))
      )
      queryClient.setQueryData<Job>(['job', jobId], (old) =>
        old
          ? {
              ...old,
              status: 'Completed' as JobStatus,
              completedAt: now,
              progress: old.progress ? { ...old.progress, percentage: 100 } : undefined,
            }
          : old
      )
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
      const now = new Date().toISOString()
      // Update job status in cache directly
      queryClient.setQueriesData<Job[]>({ queryKey: ['jobs'] }, (old) =>
        updateItemInList(old, jobId, (job) => ({
          ...job,
          status: 'Failed' as JobStatus,
          completedAt: now,
        }))
      )
      queryClient.setQueryData<Job>(['job', jobId], (old) =>
        old ? { ...old, status: 'Failed' as JobStatus, completedAt: now } : old
      )
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
      // New instance - need full data, so invalidate
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    connection.on('WorkflowInstanceCompleted', (instanceId: string) => {
      console.log('Workflow instance completed:', instanceId)
      const now = new Date().toISOString()
      // Update instance status in cache directly
      queryClient.setQueryData<WorkflowInstance[]>(['workflow-instances'], (old) =>
        updateItemInList(old, instanceId, (instance) => ({
          ...instance,
          status: 'Completed' as WorkflowInstanceStatus,
          completedAt: now,
        }))
      )
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
      const now = new Date().toISOString()
      // Update instance status in cache directly
      queryClient.setQueryData<WorkflowInstance[]>(['workflow-instances'], (old) =>
        updateItemInList(old, instanceId, (instance) => ({
          ...instance,
          status: 'Failed' as WorkflowInstanceStatus,
          completedAt: now,
        }))
      )
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
      // Update current step in cache directly
      queryClient.setQueryData<WorkflowInstance[]>(['workflow-instances'], (old) =>
        updateItemInList(old, instanceId, (instance) => ({
          ...instance,
          currentStepId: stepId,
        }))
      )
    })

    connection.on('WorkflowStepCompleted', (instanceId: string, stepId: string) => {
      console.log('Workflow step completed:', instanceId, stepId)
      // Step completed - no specific update needed, currentStepId will change on next step
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
      isConnectingRef.current = false
      setConnectionStatus('connected')
      return connection
    } catch (error) {
      console.error('SignalR connection failed:', error)
      isConnectingRef.current = false
      setConnectionStatus('disconnected')
      throw error
    }
  }, [queryClient, addNotification])

  const disconnect = useCallback(async () => {
    isConnectingRef.current = false
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
