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

// Deployment types
export interface DeploymentProfile {
  id: string
  name: string
  description?: string
  sourcePath: string
  targetAgentPattern: string
  targetPath: string
  watchForChanges: boolean
  debounceMs: number
  includePatterns?: string[]
  excludePatterns?: string[]
  deleteOrphans: boolean
  preDeployScript?: DeploymentScript
  postDeployScript?: DeploymentScript
  transferMode: FileTransferMode
  isEnabled: boolean
  createdAt: string
  lastDeployedAt?: string
}

export interface DeploymentScript {
  command: string
  arguments?: string[]
  workingDirectory?: string
  timeoutSeconds: number
  continueOnError: boolean
}

export type FileTransferMode = 'Auto' | 'Http' | 'P2P'

export interface DeploymentExecution {
  id: string
  profileId: string
  status: DeploymentStatus
  trigger: DeploymentTrigger
  startedAt: string
  completedAt?: string
  totalAgents: number
  successfulAgents: number
  failedAgents: number
  bytesTransferred: number
  filesTransferred: number
  errorMessage?: string
  agentResults?: AgentDeploymentResult[]
}

export type DeploymentStatus = 'Pending' | 'InProgress' | 'Succeeded' | 'Failed' | 'PartialSuccess' | 'Cancelled'

export type DeploymentTrigger = 'Manual' | 'FileWatch' | 'Api' | 'Scheduled'

export interface AgentDeploymentResult {
  agentId: string
  agentName: string
  status: AgentDeploymentStatus
  startedAt: string
  completedAt?: string
  errorMessage?: string
  preDeployResult?: ScriptExecutionResult
  postDeployResult?: ScriptExecutionResult
  fileSyncResult?: FileSyncExecutionResult
}

export type AgentDeploymentStatus = 'Pending' | 'RunningPreScript' | 'SyncingFiles' | 'RunningPostScript' | 'Succeeded' | 'Failed' | 'Skipped' | 'Unreachable'

export interface ScriptExecutionResult {
  success: boolean
  exitCode: number
  standardOutput?: string
  standardError?: string
  duration: string
}

export interface FileSyncExecutionResult {
  success: boolean
  filesCreated: number
  filesUpdated: number
  filesDeleted: number
  bytesTransferred: number
  transferMode: FileTransferMode
  duration: string
  errorMessage?: string
}

export interface DeploymentStatusCounts {
  pending: number
  inProgress: number
  succeeded: number
  failed: number
  partialSuccess: number
  cancelled: number
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}
