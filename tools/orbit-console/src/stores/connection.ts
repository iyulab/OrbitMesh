import { create } from 'zustand'
import { hubConnection, type ConnectionState, type HubCallbacks } from '@/lib/signalr'
import type { AgentInfo, Job, JobProgress } from '@/types/api'

type AgentEventType = 'connected' | 'disconnected' | 'statusChanged'
type JobEventType = 'created' | 'statusChanged' | 'completed'

interface AgentEvent {
  type: AgentEventType
  agentId: string
  timestamp: Date
}

interface JobEvent {
  type: JobEventType
  jobId: string
  timestamp: Date
}

interface ConnectionStore {
  state: ConnectionState
  error: string | null

  // Real-time data
  recentAgentEvents: AgentEvent[]
  recentJobEvents: JobEvent[]

  // Actions
  connect: () => Promise<void>
  disconnect: () => Promise<void>

  // Internal callbacks (for SignalR events)
  _onAgentConnected: (agent: AgentInfo) => void
  _onAgentDisconnected: (agentId: string) => void
  _onAgentStatusChanged: (agentId: string, status: string) => void
  _onJobCreated: (job: Job) => void
  _onJobStatusChanged: (jobId: string, status: string) => void
  _onJobProgress: (progress: JobProgress) => void
  _onJobCompleted: (job: Job) => void
}

const MAX_RECENT_EVENTS = 50

export const useConnectionStore = create<ConnectionStore>((set, get) => ({
  state: 'disconnected',
  error: null,
  recentAgentEvents: [],
  recentJobEvents: [],

  connect: async () => {
    const callbacks: HubCallbacks = {
      onAgentConnected: get()._onAgentConnected,
      onAgentDisconnected: get()._onAgentDisconnected,
      onAgentStatusChanged: get()._onAgentStatusChanged,
      onJobCreated: get()._onJobCreated,
      onJobStatusChanged: get()._onJobStatusChanged,
      onJobProgress: get()._onJobProgress,
      onJobCompleted: get()._onJobCompleted,
      onConnectionStateChanged: (state) => set({ state, error: null }),
    }

    try {
      await hubConnection.connect(callbacks)
    } catch (err) {
      set({ error: err instanceof Error ? err.message : 'Connection failed' })
    }
  },

  disconnect: async () => {
    await hubConnection.disconnect()
  },

  _onAgentConnected: (_agent) => {
    const newEvent: AgentEvent = { type: 'connected', agentId: _agent.id, timestamp: new Date() }
    set((state) => ({
      recentAgentEvents: [newEvent, ...state.recentAgentEvents].slice(0, MAX_RECENT_EVENTS),
    }))
  },

  _onAgentDisconnected: (agentId) => {
    const newEvent: AgentEvent = { type: 'disconnected', agentId, timestamp: new Date() }
    set((state) => ({
      recentAgentEvents: [newEvent, ...state.recentAgentEvents].slice(0, MAX_RECENT_EVENTS),
    }))
  },

  _onAgentStatusChanged: (agentId, _status) => {
    const newEvent: AgentEvent = { type: 'statusChanged', agentId, timestamp: new Date() }
    set((state) => ({
      recentAgentEvents: [newEvent, ...state.recentAgentEvents].slice(0, MAX_RECENT_EVENTS),
    }))
  },

  _onJobCreated: (job) => {
    const newEvent: JobEvent = { type: 'created', jobId: job.id, timestamp: new Date() }
    set((state) => ({
      recentJobEvents: [newEvent, ...state.recentJobEvents].slice(0, MAX_RECENT_EVENTS),
    }))
  },

  _onJobStatusChanged: (jobId, _status) => {
    const newEvent: JobEvent = { type: 'statusChanged', jobId, timestamp: new Date() }
    set((state) => ({
      recentJobEvents: [newEvent, ...state.recentJobEvents].slice(0, MAX_RECENT_EVENTS),
    }))
  },

  _onJobProgress: (_progress) => {
    // Progress updates handled separately (e.g., in job detail view)
  },

  _onJobCompleted: (job) => {
    const newEvent: JobEvent = { type: 'completed', jobId: job.id, timestamp: new Date() }
    set((state) => ({
      recentJobEvents: [newEvent, ...state.recentJobEvents].slice(0, MAX_RECENT_EVENTS),
    }))
  },
}))
