import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { getWorkflow, getWorkflowInstances } from '@/lib/api'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  WorkflowStatusBadge,
  StepStatusBadge,
  LoadingState,
  ErrorState,
} from '@/components/ui'
import { ArrowLeft, GitBranch, Clock, AlertCircle } from 'lucide-react'
import { formatDate, formatDuration } from '@/lib/utils'

export const Route = createFileRoute('/workflows/$workflowId/instances/$instanceId')({
  component: WorkflowInstanceDetailPage,
})

function WorkflowInstanceDetailPage() {
  const { workflowId, instanceId } = Route.useParams()

  const { data: workflow } = useQuery({
    queryKey: ['workflow', workflowId],
    queryFn: () => getWorkflow(workflowId),
  })

  const {
    data: instances,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['workflow-instances', workflowId],
    queryFn: () => getWorkflowInstances(workflowId),
    refetchInterval: (query) => {
      const instances = query.state.data
      const instance = instances?.find((i) => i.id === instanceId)
      // Stop polling for terminal states
      if (
        instance &&
        ['Completed', 'Failed', 'Cancelled', 'TimedOut'].includes(instance.status)
      ) {
        return false
      }
      return 2000
    },
  })

  const instance = instances?.find((i) => i.id === instanceId)

  if (isLoading) {
    return <LoadingState text="Loading instance..." />
  }

  if (error) {
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load instance'}
        onRetry={refetch}
      />
    )
  }

  if (!instance) {
    return (
      <div className="space-y-6">
        <Link
          to="/workflows/$workflowId"
          params={{ workflowId }}
          className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to workflow
        </Link>
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <p className="text-lg font-medium">Instance not found</p>
          </CardContent>
        </Card>
      </div>
    )
  }

  const duration =
    instance.startedAt && instance.completedAt
      ? new Date(instance.completedAt).getTime() - new Date(instance.startedAt).getTime()
      : null

  return (
    <div className="space-y-6">
      <Link
        to="/workflows/$workflowId"
        params={{ workflowId }}
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to workflow
      </Link>

      {/* Instance header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
            <GitBranch className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h2 className="text-2xl font-bold">Workflow Instance</h2>
            <p className="text-sm text-muted-foreground font-mono">{instance.id}</p>
          </div>
        </div>
        <WorkflowStatusBadge status={instance.status} />
      </div>

      {/* Instance details */}
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Execution Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <DetailRow
              label="Workflow"
              value={workflow?.name || instance.workflowId}
              link={{
                to: '/workflows/$workflowId',
                params: { workflowId: instance.workflowId },
              }}
            />
            <DetailRow label="Version" value={instance.workflowVersion} />
            <DetailRow label="Trigger" value={instance.triggerType || 'Manual'} />
            <DetailRow
              label="Duration"
              value={duration ? formatDuration(duration) : '-'}
            />
            <DetailRow label="Retry Count" value={instance.retryCount.toString()} />
            {instance.correlationId && (
              <DetailRow label="Correlation ID" value={instance.correlationId} />
            )}
            {instance.initiatedBy && (
              <DetailRow label="Initiated By" value={instance.initiatedBy} />
            )}
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
            <DetailRow label="Created" value={formatDate(instance.createdAt)} />
            <DetailRow
              label="Started"
              value={instance.startedAt ? formatDate(instance.startedAt) : '-'}
            />
            <DetailRow
              label="Completed"
              value={instance.completedAt ? formatDate(instance.completedAt) : '-'}
            />
          </CardContent>
        </Card>
      </div>

      {/* Error */}
      {instance.error && (
        <Card className="border-destructive/50">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm text-destructive">
              <AlertCircle className="h-4 w-4" />
              Error
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm font-mono bg-destructive/10 p-3 rounded">
              {instance.errorCode && (
                <span className="text-destructive font-bold">[{instance.errorCode}] </span>
              )}
              {instance.error}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Step Instances */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <GitBranch className="h-4 w-4" />
            Step Progress
          </CardTitle>
        </CardHeader>
        <CardContent>
          {instance.stepInstances && Object.keys(instance.stepInstances).length > 0 ? (
            <div className="space-y-3">
              {Object.entries(instance.stepInstances).map(([stepId, stepInstance]) => {
                const stepDef = workflow?.steps.find((s) => s.id === stepId)
                const stepDuration =
                  stepInstance.startedAt && stepInstance.completedAt
                    ? new Date(stepInstance.completedAt).getTime() -
                      new Date(stepInstance.startedAt).getTime()
                    : null

                return (
                  <div
                    key={stepId}
                    className="flex items-start gap-4 p-3 rounded-lg border bg-card"
                  >
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="font-medium">{stepDef?.name || stepId}</p>
                        <StepStatusBadge status={stepInstance.status} />
                      </div>
                      <p className="text-xs text-muted-foreground font-mono mt-1">
                        {stepId}
                      </p>
                      {stepInstance.startedAt && (
                        <p className="text-xs text-muted-foreground mt-1">
                          Started: {formatDate(stepInstance.startedAt)}
                          {stepDuration && ` • Duration: ${formatDuration(stepDuration)}`}
                        </p>
                      )}
                      {stepInstance.error && (
                        <p className="text-xs text-destructive mt-1">{stepInstance.error}</p>
                      )}
                      {stepInstance.jobId && (
                        <Link
                          to="/jobs/$jobId"
                          params={{ jobId: stepInstance.jobId }}
                          className="text-xs text-primary hover:underline mt-1 inline-block"
                        >
                          View Job →
                        </Link>
                      )}
                    </div>
                    {stepInstance.retryCount > 0 && (
                      <span className="text-xs text-muted-foreground">
                        Retries: {stepInstance.retryCount}
                      </span>
                    )}
                  </div>
                )
              })}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground text-center py-8">
              No step execution data available
            </p>
          )}
        </CardContent>
      </Card>

      {/* Input/Output */}
      <div className="grid gap-4 md:grid-cols-2">
        {instance.input && Object.keys(instance.input).length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Input</CardTitle>
            </CardHeader>
            <CardContent>
              <pre className="text-sm bg-muted p-3 rounded overflow-auto max-h-64">
                {JSON.stringify(instance.input, null, 2)}
              </pre>
            </CardContent>
          </Card>
        )}

        {instance.output && Object.keys(instance.output).length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Output</CardTitle>
            </CardHeader>
            <CardContent>
              <pre className="text-sm bg-muted p-3 rounded overflow-auto max-h-64">
                {JSON.stringify(instance.output, null, 2)}
              </pre>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Variables */}
      {instance.variables && Object.keys(instance.variables).length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Variables</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="text-sm bg-muted p-3 rounded overflow-auto max-h-64">
              {JSON.stringify(instance.variables, null, 2)}
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
          to={link.to as '/workflows/$workflowId'}
          params={link.params as { workflowId: string }}
          className="text-sm font-medium text-primary hover:underline"
        >
          {value}
        </Link>
      ) : (
        <span className="text-sm font-medium">{value || '-'}</span>
      )}
    </div>
  )
}
