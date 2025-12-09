import { useEffect, useRef, useCallback } from 'react'
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'

export function useSignalR() {
  const connectionRef = useRef<HubConnection | null>(null)
  const queryClient = useQueryClient()

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === 'Connected') {
      return connectionRef.current
    }

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/dashboard')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build()

    // Handle agent updates
    connection.on('AgentConnected', (agentId: string) => {
      console.log('Agent connected:', agentId)
      queryClient.invalidateQueries({ queryKey: ['agents'] })
    })

    connection.on('AgentDisconnected', (agentId: string) => {
      console.log('Agent disconnected:', agentId)
      queryClient.invalidateQueries({ queryKey: ['agents'] })
    })

    connection.on('AgentStatusChanged', (agentId: string, status: string) => {
      console.log('Agent status changed:', agentId, status)
      queryClient.invalidateQueries({ queryKey: ['agents'] })
    })

    // Handle job updates
    connection.on('JobCreated', (jobId: string) => {
      console.log('Job created:', jobId)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    })

    connection.on('JobStatusChanged', (jobId: string, status: string) => {
      console.log('Job status changed:', jobId, status)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    })

    connection.on('JobProgress', (jobId: string, progress: number, message: string) => {
      console.log('Job progress:', jobId, progress, message)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    })

    connection.on('JobCompleted', (jobId: string) => {
      console.log('Job completed:', jobId)
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    })

    // Handle workflow updates
    connection.on('WorkflowInstanceStarted', (instanceId: string, workflowId: string) => {
      console.log('Workflow instance started:', instanceId, workflowId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    connection.on('WorkflowInstanceCompleted', (instanceId: string) => {
      console.log('Workflow instance completed:', instanceId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    connection.on('WorkflowStepStarted', (instanceId: string, stepId: string) => {
      console.log('Workflow step started:', instanceId, stepId)
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
    })

    // Handle reconnection events
    connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting...', error)
    })

    connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId)
    })

    connection.onclose((error) => {
      console.warn('SignalR connection closed:', error)
    })

    try {
      await connection.start()
      console.log('SignalR connected')
      connectionRef.current = connection
      return connection
    } catch (error) {
      console.error('SignalR connection failed:', error)
      throw error
    }
  }, [queryClient])

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      await connectionRef.current.stop()
      connectionRef.current = null
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
    connect,
    disconnect,
  }
}
