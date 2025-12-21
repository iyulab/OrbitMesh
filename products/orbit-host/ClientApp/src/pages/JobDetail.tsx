import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft,
  PlayCircle,
  RefreshCw,
  Clock,
  Server,
  FileJson,
  AlertCircle,
  CheckCircle,
  XCircle,
} from 'lucide-react'
import { getJob, cancelJob } from '@/api/client'
import { JobStatusBadge } from '@/components/ui/status-badge'
import { Button } from '@/components/ui/button'
import { toast } from '@/components/ui/sonner'
import type { JobStatus } from '@/types'

export default function JobDetail() {
  const { jobId } = useParams<{ jobId: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: job, isLoading, error } = useQuery({
    queryKey: ['job', jobId],
    queryFn: () => getJob(jobId!),
    enabled: !!jobId,
    // SignalR handles real-time updates (JobStatusChanged, JobProgress, JobCompleted, JobFailed)
  })

  const cancelMutation = useMutation({
    mutationFn: cancelJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      toast.success('Job cancelled')
    },
    onError: (error) => {
      toast.error('Failed to cancel job', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const canCancel = (status: JobStatus) =>
    ['Pending', 'Assigned', 'Acknowledged', 'Running'].includes(status)

  if (isLoading) {
    return (
      <div className="p-6 flex items-center justify-center">
        <RefreshCw className="w-8 h-8 text-slate-400 animate-spin" />
      </div>
    )
  }

  if (error || !job) {
    return (
      <div className="p-6">
        <div className="text-center py-12">
          <PlayCircle className="w-12 h-12 text-slate-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">Job not found</h3>
          <p className="text-slate-500 dark:text-slate-400 mb-4">
            The requested job could not be found.
          </p>
          <Button onClick={() => navigate('/jobs')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Jobs
          </Button>
        </div>
      </div>
    )
  }

  const getStatusIcon = () => {
    switch (job.status) {
      case 'Completed':
        return <CheckCircle className="w-8 h-8 text-green-500" />
      case 'Failed':
      case 'Cancelled':
      case 'TimedOut':
        return <XCircle className="w-8 h-8 text-red-500" />
      case 'Running':
        return <RefreshCw className="w-8 h-8 text-yellow-500 animate-spin" />
      default:
        return <Clock className="w-8 h-8 text-slate-400" />
    }
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/jobs')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back
        </Button>
        <div className="h-6 w-px bg-border" />
        <div className="flex items-center gap-3">
          <div className="p-2 bg-orbit-100 dark:bg-orbit-600/20 rounded-lg">
            <PlayCircle className="w-6 h-6 text-orbit-600 dark:text-orbit-400" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-slate-900 dark:text-white">{job.command}</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400 font-mono">{job.id}</p>
          </div>
        </div>
        <div className="ml-auto flex items-center gap-3">
          <JobStatusBadge status={job.status} />
          {canCancel(job.status) && (
            <Button
              variant="outline"
              onClick={() => cancelMutation.mutate(job.id)}
              disabled={cancelMutation.isPending}
              className="text-red-600 hover:text-red-500"
            >
              Cancel Job
            </Button>
          )}
        </div>
      </div>

      {/* Status Overview */}
      <div className="card">
        <div className="flex items-center gap-6">
          {getStatusIcon()}
          <div className="flex-1 grid grid-cols-4 gap-6">
            <div>
              <p className="text-sm text-slate-500 dark:text-slate-400">Status</p>
              <JobStatusBadge status={job.status} className="mt-1" />
            </div>
            <div>
              <p className="text-sm text-slate-500 dark:text-slate-400">Priority</p>
              <p className="text-lg font-semibold text-slate-900 dark:text-white">{job.priority}</p>
            </div>
            <div>
              <p className="text-sm text-slate-500 dark:text-slate-400">Agent</p>
              {job.agentId ? (
                <Link
                  to={`/agents/${job.agentId}`}
                  className="text-orbit-600 dark:text-orbit-400 hover:text-orbit-500 font-medium"
                >
                  {job.agentId.substring(0, 8)}...
                </Link>
              ) : (
                <p className="text-slate-400">Not assigned</p>
              )}
            </div>
            <div>
              <p className="text-sm text-slate-500 dark:text-slate-400">Duration</p>
              <p className="text-lg font-semibold text-slate-900 dark:text-white">
                {job.result?.duration || '-'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Progress */}
      {job.progress && (
        <div className="card">
          <div className="flex items-center gap-3 mb-4">
            <RefreshCw className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Progress</h2>
          </div>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-slate-600 dark:text-slate-400">
                {job.progress.message || 'Processing...'}
              </span>
              <span className="font-medium text-slate-900 dark:text-white">
                {job.progress.percentage}%
              </span>
            </div>
            <div className="w-full h-3 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
              <div
                className="h-full bg-orbit-500 transition-all duration-500"
                style={{ width: `${job.progress.percentage}%` }}
              />
            </div>
            {job.progress.totalSteps && (
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Step {job.progress.currentStep || 0} of {job.progress.totalSteps}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Timeline */}
      <div className="card">
        <div className="flex items-center gap-3 mb-4">
          <Clock className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Timeline</h2>
        </div>
        <div className="space-y-4">
          <div className="flex items-center gap-4">
            <div className="w-3 h-3 bg-slate-400 rounded-full" />
            <div>
              <p className="text-slate-900 dark:text-white font-medium">Created</p>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                {new Date(job.createdAt).toLocaleString()}
              </p>
            </div>
          </div>
          {job.startedAt && (
            <div className="flex items-center gap-4">
              <div className="w-3 h-3 bg-yellow-500 rounded-full" />
              <div>
                <p className="text-slate-900 dark:text-white font-medium">Started</p>
                <p className="text-sm text-slate-500 dark:text-slate-400">
                  {new Date(job.startedAt).toLocaleString()}
                </p>
              </div>
            </div>
          )}
          {job.completedAt && (
            <div className="flex items-center gap-4">
              <div className={`w-3 h-3 rounded-full ${
                job.status === 'Completed' ? 'bg-green-500' : 'bg-red-500'
              }`} />
              <div>
                <p className="text-slate-900 dark:text-white font-medium">
                  {job.status === 'Completed' ? 'Completed' : 'Ended'}
                </p>
                <p className="text-sm text-slate-500 dark:text-slate-400">
                  {new Date(job.completedAt).toLocaleString()}
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Payload */}
      {job.payload && Object.keys(job.payload).length > 0 && (
        <div className="card">
          <div className="flex items-center gap-3 mb-4">
            <FileJson className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Payload</h2>
          </div>
          <pre className="bg-slate-100 dark:bg-slate-900 rounded-lg p-4 text-sm text-slate-700 dark:text-slate-300 overflow-x-auto font-mono">
            {JSON.stringify(job.payload, null, 2)}
          </pre>
        </div>
      )}

      {/* Result */}
      {job.result && (
        <div className="card">
          <div className="flex items-center gap-3 mb-4">
            {job.status === 'Completed' ? (
              <CheckCircle className="w-5 h-5 text-green-500" />
            ) : (
              <AlertCircle className="w-5 h-5 text-red-500" />
            )}
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Result</h2>
          </div>

          {job.result.error ? (
            <div className="bg-red-50 dark:bg-red-950/20 border border-red-200 dark:border-red-800 rounded-lg p-4">
              <p className="text-red-700 dark:text-red-400 font-medium mb-2">
                {job.result.errorCode && `[${job.result.errorCode}] `}
                Error
              </p>
              <p className="text-red-600 dark:text-red-400 text-sm">{job.result.error}</p>
            </div>
          ) : job.result.data ? (
            <pre className="bg-slate-100 dark:bg-slate-900 rounded-lg p-4 text-sm text-slate-700 dark:text-slate-300 overflow-x-auto font-mono">
              {JSON.stringify(job.result.data, null, 2)}
            </pre>
          ) : (
            <p className="text-slate-500 dark:text-slate-400">No result data</p>
          )}
        </div>
      )}

      {/* Agent Info */}
      {job.agentId && (
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-3">
              <Server className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Assigned Agent</h2>
            </div>
            <Link
              to={`/agents/${job.agentId}`}
              className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500"
            >
              View agent →
            </Link>
          </div>
          <div className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <p className="font-mono text-sm text-slate-900 dark:text-white">{job.agentId}</p>
          </div>
        </div>
      )}
    </div>
  )
}
