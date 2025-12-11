import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import {
  Clock,
  CheckCircle,
  XCircle,
  Activity,
  AlertTriangle,
  Pause,
  Square,
  AlertCircle,
  Loader2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import type { AgentStatus, JobStatus, WorkflowInstanceStatus } from '@/types'

const statusBadgeVariants = cva(
  'inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors',
  {
    variants: {
      variant: {
        // Success states
        success: 'bg-green-500/20 text-green-600 dark:text-green-400',
        // Warning states
        warning: 'bg-yellow-500/20 text-yellow-600 dark:text-yellow-400',
        // Error states
        error: 'bg-red-500/20 text-red-600 dark:text-red-400',
        // Info states
        info: 'bg-blue-500/20 text-blue-600 dark:text-blue-400',
        // Neutral states
        neutral: 'bg-slate-500/20 text-slate-600 dark:text-slate-400',
        // Orange states
        orange: 'bg-orange-500/20 text-orange-600 dark:text-orange-400',
      },
    },
    defaultVariants: {
      variant: 'neutral',
    },
  }
)

// Agent status configuration
const agentStatusConfig: Record<
  AgentStatus,
  { variant: 'success' | 'warning' | 'error' | 'info' | 'neutral' | 'orange'; icon: React.ComponentType<{ className?: string }> }
> = {
  Ready: { variant: 'success', icon: CheckCircle },
  Running: { variant: 'warning', icon: Activity },
  Created: { variant: 'neutral', icon: Clock },
  Initializing: { variant: 'info', icon: Loader2 },
  Paused: { variant: 'info', icon: Pause },
  Stopping: { variant: 'orange', icon: Square },
  Stopped: { variant: 'neutral', icon: Square },
  Faulted: { variant: 'error', icon: AlertCircle },
  Disconnected: { variant: 'error', icon: XCircle },
}

// Job status configuration
const jobStatusConfig: Record<
  JobStatus,
  { variant: 'success' | 'warning' | 'error' | 'info' | 'neutral' | 'orange'; icon: React.ComponentType<{ className?: string }> }
> = {
  Pending: { variant: 'neutral', icon: Clock },
  Assigned: { variant: 'info', icon: Activity },
  Acknowledged: { variant: 'info', icon: Activity },
  Running: { variant: 'warning', icon: Activity },
  Completed: { variant: 'success', icon: CheckCircle },
  Failed: { variant: 'error', icon: XCircle },
  Cancelled: { variant: 'neutral', icon: XCircle },
  TimedOut: { variant: 'orange', icon: AlertTriangle },
}

// Workflow instance status configuration
const workflowStatusConfig: Record<
  WorkflowInstanceStatus,
  { variant: 'success' | 'warning' | 'error' | 'info' | 'neutral' | 'orange'; icon: React.ComponentType<{ className?: string }> }
> = {
  Running: { variant: 'warning', icon: Activity },
  Completed: { variant: 'success', icon: CheckCircle },
  Failed: { variant: 'error', icon: XCircle },
  Paused: { variant: 'info', icon: Pause },
  Cancelled: { variant: 'neutral', icon: XCircle },
}

export interface StatusBadgeProps extends VariantProps<typeof statusBadgeVariants> {
  className?: string
  showIcon?: boolean
}

export interface AgentStatusBadgeProps extends StatusBadgeProps {
  status: AgentStatus
}

export interface JobStatusBadgeProps extends StatusBadgeProps {
  status: JobStatus
}

export interface WorkflowStatusBadgeProps extends StatusBadgeProps {
  status: WorkflowInstanceStatus
}

export function AgentStatusBadge({
  status,
  className,
  showIcon = true,
}: AgentStatusBadgeProps) {
  const config = agentStatusConfig[status] || agentStatusConfig.Created
  const Icon = config.icon

  return (
    <span
      className={cn(statusBadgeVariants({ variant: config.variant }), className)}
      role="status"
      aria-label={`Agent status: ${status}`}
    >
      {showIcon && <Icon className={cn('w-3 h-3', status === 'Initializing' && 'animate-spin')} aria-hidden="true" />}
      {status}
    </span>
  )
}

export function JobStatusBadge({
  status,
  className,
  showIcon = true,
}: JobStatusBadgeProps) {
  const config = jobStatusConfig[status] || jobStatusConfig.Pending
  const Icon = config.icon

  return (
    <span
      className={cn(statusBadgeVariants({ variant: config.variant }), className)}
      role="status"
      aria-label={`Job status: ${status}`}
    >
      {showIcon && <Icon className={cn('w-3 h-3', status === 'Running' && 'animate-pulse')} aria-hidden="true" />}
      {status}
    </span>
  )
}

export function WorkflowStatusBadge({
  status,
  className,
  showIcon = true,
}: WorkflowStatusBadgeProps) {
  const config = workflowStatusConfig[status] || workflowStatusConfig.Running
  const Icon = config.icon

  return (
    <span
      className={cn(statusBadgeVariants({ variant: config.variant }), className)}
      role="status"
      aria-label={`Workflow status: ${status}`}
    >
      {showIcon && <Icon className={cn('w-3 h-3', status === 'Running' && 'animate-pulse')} aria-hidden="true" />}
      {status}
    </span>
  )
}

// Generic status badge for custom use cases
export interface GenericStatusBadgeProps extends StatusBadgeProps {
  status: string
  icon?: React.ComponentType<{ className?: string }>
}

export function StatusBadge({
  status,
  variant = 'neutral',
  icon: Icon,
  className,
  showIcon = true,
}: GenericStatusBadgeProps) {
  return (
    <span className={cn(statusBadgeVariants({ variant }), className)}>
      {showIcon && Icon && <Icon className="w-3 h-3" />}
      {status}
    </span>
  )
}
