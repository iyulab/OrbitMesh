import { Badge } from './Badge'
import type { JobStatus, AgentStatus } from '@/types/api'

const jobStatusConfig: Record<JobStatus, { variant: 'default' | 'success' | 'warning' | 'destructive' | 'outline'; label: string }> = {
  Pending: { variant: 'outline', label: 'Pending' },
  Assigned: { variant: 'default', label: 'Assigned' },
  Running: { variant: 'warning', label: 'Running' },
  Completed: { variant: 'success', label: 'Completed' },
  Failed: { variant: 'destructive', label: 'Failed' },
  Cancelled: { variant: 'outline', label: 'Cancelled' },
  TimedOut: { variant: 'destructive', label: 'Timed Out' },
}

const agentStatusConfig: Record<AgentStatus, { variant: 'default' | 'success' | 'warning' | 'destructive' | 'outline'; label: string }> = {
  Created: { variant: 'outline', label: 'Created' },
  Initializing: { variant: 'default', label: 'Initializing' },
  Ready: { variant: 'success', label: 'Ready' },
  Running: { variant: 'warning', label: 'Running' },
  Paused: { variant: 'outline', label: 'Paused' },
  Stopping: { variant: 'warning', label: 'Stopping' },
  Stopped: { variant: 'outline', label: 'Stopped' },
  Faulted: { variant: 'destructive', label: 'Faulted' },
  Disconnected: { variant: 'destructive', label: 'Disconnected' },
}

interface JobStatusBadgeProps {
  status: JobStatus
}

export function JobStatusBadge({ status }: JobStatusBadgeProps) {
  const config = jobStatusConfig[status]
  return <Badge variant={config.variant}>{config.label}</Badge>
}

interface AgentStatusBadgeProps {
  status: AgentStatus
}

export function AgentStatusBadge({ status }: AgentStatusBadgeProps) {
  const config = agentStatusConfig[status]
  return <Badge variant={config.variant}>{config.label}</Badge>
}
