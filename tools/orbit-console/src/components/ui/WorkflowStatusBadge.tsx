import { Badge } from './Badge'
import type { WorkflowStatus, StepStatus } from '@/types/workflow'

const workflowStatusConfig: Record<WorkflowStatus, { variant: 'default' | 'success' | 'warning' | 'destructive' | 'outline'; label: string }> = {
  Pending: { variant: 'outline', label: 'Pending' },
  Running: { variant: 'warning', label: 'Running' },
  Completed: { variant: 'success', label: 'Completed' },
  Failed: { variant: 'destructive', label: 'Failed' },
  Cancelled: { variant: 'outline', label: 'Cancelled' },
  TimedOut: { variant: 'destructive', label: 'Timed Out' },
  Paused: { variant: 'default', label: 'Paused' },
  Compensating: { variant: 'warning', label: 'Compensating' },
}

const stepStatusConfig: Record<StepStatus, { variant: 'default' | 'success' | 'warning' | 'destructive' | 'outline'; label: string }> = {
  Pending: { variant: 'outline', label: 'Pending' },
  WaitingForDependencies: { variant: 'outline', label: 'Waiting' },
  Running: { variant: 'warning', label: 'Running' },
  Completed: { variant: 'success', label: 'Completed' },
  Failed: { variant: 'destructive', label: 'Failed' },
  Skipped: { variant: 'outline', label: 'Skipped' },
  Cancelled: { variant: 'outline', label: 'Cancelled' },
  TimedOut: { variant: 'destructive', label: 'Timed Out' },
  WaitingForEvent: { variant: 'default', label: 'Waiting Event' },
  WaitingForApproval: { variant: 'default', label: 'Pending Approval' },
  Compensating: { variant: 'warning', label: 'Compensating' },
  Compensated: { variant: 'success', label: 'Compensated' },
}

interface WorkflowStatusBadgeProps {
  status: WorkflowStatus
}

export function WorkflowStatusBadge({ status }: WorkflowStatusBadgeProps) {
  const config = workflowStatusConfig[status]
  return <Badge variant={config.variant}>{config.label}</Badge>
}

interface StepStatusBadgeProps {
  status: StepStatus
}

export function StepStatusBadge({ status }: StepStatusBadgeProps) {
  const config = stepStatusConfig[status]
  return <Badge variant={config.variant}>{config.label}</Badge>
}
