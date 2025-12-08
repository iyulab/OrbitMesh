import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getJob, cancelJob, retryJob } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle, JobStatusBadge } from '@/components/ui'
import { ArrowLeft, Briefcase, Clock, XCircle, RefreshCw } from 'lucide-react'
import { formatDate, formatDuration } from '@/lib/utils'

export const Route = createFileRoute('/jobs/$jobId')({
  component: JobDetailPage,
})

function JobDetailPage() {
  const { jobId } = Route.useParams()
  const queryClient = useQueryClient()

  const { data: job, isLoading } = useQuery({
    queryKey: ['job', jobId],
    queryFn: () => getJob(jobId),
    refetchInterval: (query) => {
      const job = query.state.data
      // Stop polling for terminal states
      if (job && ['Completed', 'Failed', 'Cancelled', 'TimedOut'].includes(job.status)) {
        return false
      }
      return 2000
    },
  })

  const cancelMutation = useMutation({
    mutationFn: () => cancelJob(jobId, 'Cancelled by user'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    },
  })

  const retryMutation = useMutation({
    mutationFn: () => retryJob(jobId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['job', jobId] })
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    },
  })

  if (isLoading) {
    return <JobDetailSkeleton />
  }

  if (!job) {
    return (
      <div className="space-y-6">
        <Link
          to="/jobs"
          className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to jobs
        </Link>
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <p className="text-lg font-medium">Job not found</p>
          </CardContent>
        </Card>
      </div>
    )
  }

  const canCancel = ['Pending', 'Assigned', 'Running'].includes(job.status)
  const canRetry = ['Failed', 'TimedOut'].includes(job.status)
  const duration =
    job.startedAt && job.completedAt
      ? new Date(job.completedAt).getTime() - new Date(job.startedAt).getTime()
      : null

  return (
    <div className="space-y-6">
      <Link
        to="/jobs"
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to jobs
      </Link>

      {/* Job header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
            <Briefcase className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h2 className="text-2xl font-bold">{job.request.command}</h2>
            <p className="text-sm text-muted-foreground font-mono">{job.id}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {canCancel && (
            <button
              onClick={() => cancelMutation.mutate()}
              disabled={cancelMutation.isPending}
              className="inline-flex items-center gap-2 px-3 py-2 rounded-lg bg-destructive/10 text-destructive hover:bg-destructive/20 disabled:opacity-50 text-sm"
            >
              <XCircle className="h-4 w-4" />
              Cancel
            </button>
          )}
          {canRetry && (
            <button
              onClick={() => retryMutation.mutate()}
              disabled={retryMutation.isPending}
              className="inline-flex items-center gap-2 px-3 py-2 rounded-lg bg-primary/10 text-primary hover:bg-primary/20 disabled:opacity-50 text-sm"
            >
              <RefreshCw className="h-4 w-4" />
              Retry
            </button>
          )}
          <JobStatusBadge status={job.status} />
        </div>
      </div>

      {/* Job details */}
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Execution Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <DetailRow label="Pattern" value={job.request.pattern} />
            <DetailRow label="Priority" value={job.request.priority.toString()} />
            <DetailRow
              label="Assigned Agent"
              value={job.assignedAgentId}
              link={
                job.assignedAgentId
                  ? { to: '/agents/$agentId', params: { agentId: job.assignedAgentId } }
                  : undefined
              }
            />
            <DetailRow
              label="Duration"
              value={duration ? formatDuration(duration) : '-'}
            />
            <DetailRow
              label="Retry Count"
              value={job.retryCount.toString()}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm">
              <Clock className="h-4 w-4" />
              Timeline
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <DetailRow label="Created" value={formatDate(job.createdAt)} />
            <DetailRow
              label="Assigned"
              value={job.assignedAt ? formatDate(job.assignedAt) : '-'}
            />
            <DetailRow
              label="Started"
              value={job.startedAt ? formatDate(job.startedAt) : '-'}
            />
            <DetailRow
              label="Completed"
              value={job.completedAt ? formatDate(job.completedAt) : '-'}
            />
          </CardContent>
        </Card>
      </div>

      {/* Progress */}
      {job.lastProgress && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Progress</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">
                  {job.lastProgress.message || job.lastProgress.currentStep || 'Processing...'}
                </span>
                <span className="font-medium">{job.lastProgress.percentage}%</span>
              </div>
              <div className="h-2 bg-muted rounded-full overflow-hidden">
                <div
                  className="h-full bg-primary transition-all duration-300"
                  style={{ width: `${job.lastProgress.percentage}%` }}
                />
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Error */}
      {job.error && (
        <Card className="border-destructive/50">
          <CardHeader>
            <CardTitle className="text-sm text-destructive">Error</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm font-mono bg-destructive/10 p-3 rounded">
              {job.errorCode && (
                <span className="text-destructive font-bold">[{job.errorCode}] </span>
              )}
              {job.error}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Result */}
      {job.result?.data !== undefined && job.result?.data !== null && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Result</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="text-sm bg-muted p-3 rounded overflow-auto max-h-64">
              {JSON.stringify(job.result.data as object, null, 2)}
            </pre>
          </CardContent>
        </Card>
      )}

      {/* Request payload */}
      {job.request.payload !== undefined && job.request.payload !== null && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Request Payload</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="text-sm bg-muted p-3 rounded overflow-auto max-h-64">
              {JSON.stringify(job.request.payload as object, null, 2)}
            </pre>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function DetailRow({
  label,
  value,
  link,
}: {
  label: string
  value: string | undefined | null
  link?: { to: string; params: Record<string, string> }
}) {
  return (
    <div className="flex justify-between">
      <span className="text-sm text-muted-foreground">{label}</span>
      {link && value ? (
        <Link
          to={link.to as '/agents/$agentId'}
          params={link.params as { agentId: string }}
          className="text-sm font-medium text-primary hover:underline"
        >
          {value.slice(0, 8)}...
        </Link>
      ) : (
        <span className="text-sm font-medium">{value || '-'}</span>
      )}
    </div>
  )
}

function JobDetailSkeleton() {
  return (
    <div className="space-y-6">
      <div className="h-4 w-24 bg-muted rounded animate-pulse" />
      <div className="flex items-center gap-4">
        <div className="h-12 w-12 bg-muted rounded-lg animate-pulse" />
        <div>
          <div className="h-8 w-48 bg-muted rounded animate-pulse" />
          <div className="h-4 w-64 bg-muted rounded animate-pulse mt-2" />
        </div>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardContent className="h-48 animate-pulse bg-muted/50" />
        </Card>
        <Card>
          <CardContent className="h-48 animate-pulse bg-muted/50" />
        </Card>
      </div>
    </div>
  )
}
