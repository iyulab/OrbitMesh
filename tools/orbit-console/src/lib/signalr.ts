import * as signalR from '@microsoft/signalr'
import type { AgentInfo, Job, JobProgress } from '@/types/api'

type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

interface HubCallbacks {
  onAgentConnected?: (agent: AgentInfo) => void
  onAgentDisconnected?: (agentId: string) => void
  onAgentStatusChanged?: (agentId: string, status: string) => void
  onJobCreated?: (job: Job) => void
  onJobStatusChanged?: (jobId: string, status: string) => void
  onJobProgress?: (progress: JobProgress) => void
  onJobCompleted?: (job: Job) => void
  onConnectionStateChanged?: (state: ConnectionState) => void
}

class OrbitMeshHubConnection {
  private connection: signalR.HubConnection | null = null
  private callbacks: HubCallbacks = {}
  private state: ConnectionState = 'disconnected'

  async connect(callbacks: HubCallbacks): Promise<void> {
    this.callbacks = callbacks
    this.setState('connecting')

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/console')
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0, 2, 4, 8, 16, 32 seconds (max)
          const delay = Math.min(Math.pow(2, retryContext.previousRetryCount) * 1000, 32000)
          return delay
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build()

    this.setupEventHandlers()
    this.setupLifecycleHandlers()

    try {
      await this.connection.start()
      this.setState('connected')
    } catch (err) {
      this.setState('disconnected')
      throw err
    }
  }

  private setupEventHandlers(): void {
    if (!this.connection) return

    this.connection.on('AgentConnected', (agent: AgentInfo) => {
      this.callbacks.onAgentConnected?.(agent)
    })

    this.connection.on('AgentDisconnected', (agentId: string) => {
      this.callbacks.onAgentDisconnected?.(agentId)
    })

    this.connection.on('AgentStatusChanged', (agentId: string, status: string) => {
      this.callbacks.onAgentStatusChanged?.(agentId, status)
    })

    this.connection.on('JobCreated', (job: Job) => {
      this.callbacks.onJobCreated?.(job)
    })

    this.connection.on('JobStatusChanged', (jobId: string, status: string) => {
      this.callbacks.onJobStatusChanged?.(jobId, status)
    })

    this.connection.on('JobProgress', (progress: JobProgress) => {
      this.callbacks.onJobProgress?.(progress)
    })

    this.connection.on('JobCompleted', (job: Job) => {
      this.callbacks.onJobCompleted?.(job)
    })
  }

  private setupLifecycleHandlers(): void {
    if (!this.connection) return

    this.connection.onreconnecting(() => {
      this.setState('reconnecting')
    })

    this.connection.onreconnected(() => {
      this.setState('connected')
    })

    this.connection.onclose(() => {
      this.setState('disconnected')
    })
  }

  private setState(state: ConnectionState): void {
    this.state = state
    this.callbacks.onConnectionStateChanged?.(state)
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
    }
    this.setState('disconnected')
  }

  getState(): ConnectionState {
    return this.state
  }
}

export const hubConnection = new OrbitMeshHubConnection()
export type { ConnectionState, HubCallbacks }
