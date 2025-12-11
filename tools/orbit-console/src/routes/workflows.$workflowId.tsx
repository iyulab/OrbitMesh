import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getWorkflow, getWorkflowInstances, startWorkflow } from '@/lib/api'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Button,
  WorkflowStatusBadge,
  LoadingState,
  ErrorState,
} from '@/components/ui'
import { ArrowLeft, GitBranch, Play, Edit, Clock, Settings } from 'lucide-react'
import { formatDate } from '@/lib/utils'
import { toast } from 'sonner'

export const Route = createFileRoute('/workflows/$workflowId')({
  component: WorkflowDetailPage,
})

function WorkflowDetailPage() {
  const { workflowId } = Route.useParams()
  const queryClient = useQueryClient()

  const {
    data: workflow,
    isLoading: workflowLoading,
    error: workflowError,
    refetch: refetchWorkflow,
  } = useQuery({
    queryKey: ['workflow', workflowId],
    queryFn: () => getWorkflow(workflowId),
  })

  const { data: instances } = useQuery({
    queryKey: ['workflow-instances', workflowId],
    queryFn: () => getWorkflowInstances(workflowId),
    enabled: !!workflow,
    refetchInterval: 5000,
  })

  const startMutation = useMutation({
    mutationFn: () => startWorkflow(workflowId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workflow-instances', workflowId] })
      toast.success('Workflow started successfully')
    },
    onError: (error) => {
      toast.error(`Failed to start workflow: ${error instanceof Error ? error.message : 'Unknown error'}`)
    },
  })

  if (workflowLoading) {
    return <LoadingState text="Loading workflow..." />
  }

  if (workflowError) {
    return (
      <ErrorState
        message={workflowError instanceof Error ? workflowError.message : 'Failed to load workflow'}
        onRetry={refetchWorkflow}
      />
    )
  }

  if (!workflow) {
    return (
      <div className="space-y-6">
        <Link
          to="/workflows"
          className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to workflows
        </Link>
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <p className="text-lg font-medium">Workflow not found</p>
          </CardContent>
        </Card>
      </div>
    )
  }

  const recentInstances = instances
    ?.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
    .slice(0, 5)

  return (
    <div className="space-y-6">
      <Link
        to="/workflows"
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to workflows
      </Link>

      {/* Workflow header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
            <GitBranch className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h2 className="text-2xl font-bold">{workflow.name}</h2>
            <p className="text-sm text-muted-foreground font-mono">v{workflow.version}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Link to="/workflows/$workflowId/edit" params={{ workflowId }}>
            <Button variant="outline">
              <Edit className="h-4 w-4" />
              Edit
            </Button>
          </Link>
          <Button onClick={() => startMutation.mutate()} disabled={startMutation.isPending}>
            <Play className="h-4 w-4" />
            {startMutation.isPending ? 'Starting...' : 'Run'}
          </Button>
        </div>
      </div>

      {/* Workflow details */}
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm flex items-center gap-2">
              <Settings className="h-4 w-4" />
              Details
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <DetailRow label="ID" value={workflow.id} />
            <DetailRow label="Version" value={workflow.version} />
            <DetailRow
              label="Status"
              value={workflow.isEnabled ? 'Enabled' : 'Disabled'}
            />
            <DetailRow label="Steps" value={workflow.steps.length.toString()} />
            <DetailRow
              label="Created"
              value={formatDate(workflow.createdAt)}
            />
            {workflow.modifiedAt && (
              <DetailRow
                label="Modified"
                value={formatDate(workflow.modifiedAt)}
              />
            )}
            {workflow.timeout && (
              <DetailRow label="Timeout" value={workflow.timeout} />
            )}
            {(workflow.maxRetries ?? 0) > 0 && (
              <DetailRow label="Max Retries" value={workflow.maxRetries?.toString()} />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Description & Tags</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {workflow.description ? (
              <p className="text-sm text-muted-foreground">{workflow.description}</p>
            ) : (
              <p className="text-sm text-muted-foreground italic">No description</p>
            )}

            {workflow.tags && workflow.tags.length > 0 && (
              <div>
                <p className="text-xs text-muted-foreground mb-2">Tags</p>
                <div className="flex flex-wrap gap-1">
                  {workflow.tags.map((tag) => (
                    <span
                      key={tag}
                      className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Steps */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <GitBranch className="h-4 w-4" />
            Steps ({workflow.steps.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {workflow.steps.map((step, index) => (
              <div
                key={step.id}
                className="flex items-start gap-4 p-3 rounded-lg border bg-card"
              >
                <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary text-sm font-medium">
                  {index + 1}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="font-medium">{step.name}</p>
                    <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
                      {step.type}
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground font-mono mt-1">{step.id}</p>
                  {step.dependsOn && step.dependsOn.length > 0 && (
                    <p className="text-xs text-muted-foreground mt-1">
                      Depends on: {step.dependsOn.join(', ')}
                    </p>
                  )}
                </div>
                {step.condition && (
                  <span className="text-xs text-muted-foreground">
                    Conditional
                  </span>
                )}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Recent Instances */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Clock className="h-4 w-4" />
            Recent Executions
          </CardTitle>
        </CardHeader>
        <CardContent>
          {!recentInstances || recentInstances.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">
              No executions yet
            </p>
          ) : (
            <div className="space-y-2">
              {recentInstances.map((instance) => (
                <Link
                  key={instance.id}
                  to="/workflows/$workflowId/instances/$instanceId"
                  params={{ workflowId, instanceId: instance.id }}
                  className="flex items-center justify-between p-3 rounded-lg hover:bg-muted transition-colors"
                >
                  <div>
                    <p className="font-medium font-mono text-sm">{instance.id}</p>
                    <p className="text-xs text-muted-foreground">
                      {instance.triggerType || 'Manual'} • {formatDate(instance.createdAt)}
                    </p>
                  </div>
                  <WorkflowStatusBadge status={instance.status} />
                </Link>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function DetailRow({
  label,
  value,
}: {
  label: string
  value: string | undefined | null
}) {
  return (
    <div className="flex justify-between">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value || '-'}</span>
    </div>
  )
}
