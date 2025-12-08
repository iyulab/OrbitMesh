// API Types matching OrbitMesh.Core models

export type JobStatus =
  | 'Pending'
  | 'Assigned'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'
  | 'TimedOut'

export type AgentStatus =
  | 'Created'
  | 'Initializing'
  | 'Ready'
  | 'Running'
  | 'Paused'
  | 'Stopping'
  | 'Stopped'
  | 'Faulted'
  | 'Disconnected'

export type ExecutionPattern =
  | 'FireAndForget'
  | 'RequestResponse'
  | 'Scatter'
  | 'ScatterGather'
  | 'Broadcast'
  | 'Pipeline'

export interface AgentInfo {
  id: string
  name: string
  status: AgentStatus
  group?: string
  hostname?: string
  version?: string
  tags: string[]
  capabilities: string[]
  metadata: Record<string, string>
  registeredAt: string
  lastHeartbeat?: string
}

export interface JobRequest {
  id: string
  command: string
  payload?: unknown
  priority: number
  pattern: ExecutionPattern
  timeout?: number
  targetAgentId?: string
  targetAgentGroup?: string
  requiredCapabilities: string[]
  metadata: Record<string, string>
  createdAt: string
}

export interface JobProgress {
  jobId: string
  percentage: number
  message?: string
  currentStep?: string
  totalSteps?: number
  timestamp: string
}

export interface JobResult {
  jobId: string
  status: JobStatus
  data?: unknown
  error?: string
  errorCode?: string
  startedAt?: string
  completedAt?: string
  executionTimeMs?: number
}

export interface Job {
  id: string
  request: JobRequest
  status: JobStatus
  assignedAgentId?: string
  createdAt: string
  assignedAt?: string
  startedAt?: string
  completedAt?: string
  result?: JobResult
  lastProgress?: JobProgress
  error?: string
  errorCode?: string
  retryCount: number
  timeoutCount: number
  cancellationReason?: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface DashboardStats {
  totalAgents: number
  onlineAgents: number
  totalJobs: number
  runningJobs: number
  completedJobs: number
  failedJobs: number
  jobsByStatus: Record<JobStatus, number>
  agentsByStatus: Record<AgentStatus, number>
}
