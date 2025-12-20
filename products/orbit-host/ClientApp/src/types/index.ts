// Agent types
export interface Agent {
  id: string
  name: string
  status: AgentStatus
  group?: string
  connectionId?: string
  capabilities: Capability[]
  lastHeartbeat?: string
  metadata?: Record<string, string>
}

export type AgentStatus = 'Created' | 'Initializing' | 'Ready' | 'Running' | 'Paused' | 'Stopping' | 'Stopped' | 'Faulted' | 'Disconnected'

export interface Capability {
  name: string
  version?: string
  parameters?: ParameterDefinition[]
}

export interface ParameterDefinition {
  name: string
  type: string
  isRequired: boolean
  description?: string
  defaultValue?: string
}

// Job types
export interface Job {
  id: string
  command: string
  status: JobStatus
  agentId?: string
  priority: number
  payload?: object
  result?: JobResult
  createdAt: string
  startedAt?: string
  completedAt?: string
  progress?: JobProgress
}

export type JobStatus = 'Pending' | 'Assigned' | 'Acknowledged' | 'Running' | 'Completed' | 'Failed' | 'Cancelled' | 'TimedOut'

export interface JobResult {
  jobId: string
  agentId: string
  status: JobStatus
  data?: object
  error?: string
  errorCode?: string
  duration: string
}

export interface JobProgress {
  jobId: string
  percentage: number
  message?: string
  currentStep?: number
  totalSteps?: number
}

// Workflow types
export interface Workflow {
  id: string
  name: string
  description?: string
  version: string
  isActive: boolean
  steps: WorkflowStep[]
}

export interface WorkflowStep {
  id: string
  type: string
  name?: string
  config: Record<string, unknown>
}

export interface WorkflowInstance {
  id: string
  workflowId: string
  status: WorkflowInstanceStatus
  startedAt: string
  completedAt?: string
  currentStepId?: string
  variables: Record<string, unknown>
}

export type WorkflowInstanceStatus = 'Running' | 'Completed' | 'Failed' | 'Paused' | 'Cancelled'

// Server status
export interface ServerStatus {
  name: string
  version: string
  status: string
  uptime: string
  agents: {
    total: number
    ready: number
    busy: number
    disconnected: number
  }
  jobs: {
    pending: number
    running: number
    completed: number
    failed: number
  }
}

// API Token
export interface ApiToken {
  id: string
  name: string
  token?: string
  createdAt: string
  expiresAt?: string
  lastUsedAt?: string
  scopes: string[]
}

// Bootstrap Token (TOFU enrollment) - Single reusable token
export interface BootstrapToken {
  id: string
  token?: string // Only returned on regenerate
  isEnabled: boolean
  autoApprove: boolean
  createdAt: string
  lastRegeneratedAt?: string
}
