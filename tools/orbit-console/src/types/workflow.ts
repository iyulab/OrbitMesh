// Workflow Types matching OrbitMesh.Workflows models

export type WorkflowStatus =
  | 'Pending'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'
  | 'TimedOut'
  | 'Paused'
  | 'Compensating'

export type StepStatus =
  | 'Pending'
  | 'WaitingForDependencies'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Skipped'
  | 'Cancelled'
  | 'TimedOut'
  | 'WaitingForEvent'
  | 'WaitingForApproval'
  | 'Compensating'
  | 'Compensated'

export type StepType =
  | 'Job'
  | 'Parallel'
  | 'Conditional'
  | 'Delay'
  | 'WaitForEvent'
  | 'SubWorkflow'
  | 'ForEach'
  | 'Transform'
  | 'Notify'
  | 'Approval'
  | 'Log'

export interface StepConfig {
  command?: string
  payload?: unknown
  agentId?: string
  agentGroup?: string
  requiredCapabilities?: string[]
  timeout?: string
  steps?: WorkflowStep[]
  branches?: WorkflowStep[][]
  condition?: string
  duration?: string
  eventName?: string
  workflowId?: string
  collection?: string
  itemVariable?: string
  expression?: string
  message?: string
  channel?: string
  approvers?: string[]
  [key: string]: unknown
}

export interface CompensationConfig {
  config: StepConfig
  timeout?: string
  maxRetries?: number
}

export interface WorkflowStep {
  id: string
  name: string
  type: StepType
  config: StepConfig
  dependsOn?: string[]
  condition?: string
  timeout?: string
  maxRetries?: number
  retryDelay?: string
  continueOnError?: boolean
  compensation?: CompensationConfig
  outputVariable?: string
}

export interface WorkflowErrorHandling {
  strategy: 'StopOnFirstError' | 'ContinueAndAggregate' | 'Compensate'
  compensationWorkflowId?: string
  continueOnError?: boolean
}

export interface WorkflowDefinition {
  id: string
  name: string
  version: string
  description?: string
  steps: WorkflowStep[]
  triggers?: WorkflowTrigger[]
  variables?: Record<string, unknown>
  timeout?: string
  maxRetries?: number
  tags?: string[]
  isEnabled: boolean
  createdAt: string
  modifiedAt?: string
  errorHandling?: WorkflowErrorHandling
}

export interface WorkflowTrigger {
  id: string
  type: 'Schedule' | 'Event' | 'Manual' | 'Webhook' | 'JobCompletion' | 'FileWatch'
  isEnabled: boolean
  config?: Record<string, unknown>
}

export interface StepInstance {
  stepId: string
  status: StepStatus
  startedAt?: string
  completedAt?: string
  output?: unknown
  error?: string
  retryCount: number
  jobId?: string
  subWorkflowInstanceId?: string
}

export interface WorkflowInstance {
  id: string
  workflowId: string
  workflowVersion: string
  status: WorkflowStatus
  triggerId?: string
  triggerType?: string
  input?: Record<string, unknown>
  variables?: Record<string, unknown>
  output?: Record<string, unknown>
  stepInstances?: Record<string, StepInstance>
  createdAt: string
  startedAt?: string
  completedAt?: string
  error?: string
  errorCode?: string
  retryCount: number
  parentInstanceId?: string
  parentStepId?: string
  correlationId?: string
  initiatedBy?: string
}
