import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { getWorkflows, getWorkflowInstances } from '@/lib/api'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Button,
  WorkflowStatusBadge,
  EmptyState,
  LoadingState,
  ErrorState,
} from '@/components/ui'
import { GitBranch, Plus, Play, ExternalLink, Clock } from 'lucide-react'
import { formatDate } from '@/lib/utils'

export const Route = createFileRoute('/workflows')({
  component: WorkflowsPage,
})

function WorkflowsPage() {
  const {
    data: workflows,
    isLoading: workflowsLoading,
    error: workflowsError,
    refetch: refetchWorkflows,
  } = useQuery({
    queryKey: ['workflows'],
    queryFn: getWorkflows,
  })

  const {
    data: instances,
    isLoading: instancesLoading,
    error: instancesError,
  } = useQuery({
    queryKey: ['workflow-instances'],
    queryFn: () => getWorkflowInstances(),
    refetchInterval: 5000,
  })

  if (workflowsLoading || instancesLoading) {
    return <LoadingState text="Loading workflows..." />
  }

  if (workflowsError || instancesError) {
    return (
      <ErrorState
        message={
          workflowsError instanceof Error
            ? workflowsError.message
            : instancesError instanceof Error
              ? instancesError.message
              : 'Failed to load workflows'
        }
        onRetry={refetchWorkflows}
      />
    )
  }

  const recentInstances = instances
    ?.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
    .slice(0, 10)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Workflows</h2>
          <p className="text-muted-foreground">
            {workflows?.length ?? 0} workflow definitions
          </p>
        </div>
        <Link to="/workflows/new">
          <Button>
            <Plus className="h-4 w-4" />
            New Workflow
          </Button>
        </Link>
      </div>

      {/* Workflow Definitions */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Definitions</h3>
        {!workflows || workflows.length === 0 ? (
          <Card>
            <CardContent>
              <EmptyState
                icon={GitBranch}
                title="No workflows defined"
                description="Create your first workflow to orchestrate complex operations"
                action={
                  <Link to="/workflows/new">
                    <Button>
                      <Plus className="h-4 w-4" />
                      Create Workflow
                    </Button>
                  </Link>
                }
              />
            </CardContent>
          </Card>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {workflows.map((workflow) => (
              <Card key={workflow.id} className="hover:border-primary/50 transition-colors">
                <CardHeader className="pb-3">
                  <div className="flex items-start justify-between">
                    <div className="space-y-1">
                      <CardTitle className="text-base">{workflow.name}</CardTitle>
                      <p className="text-xs text-muted-foreground font-mono">
                        v{workflow.version}
                      </p>
                    </div>
                    <span
                      className={`inline-flex items-center rounded px-2 py-0.5 text-xs ${
                        workflow.isEnabled
                          ? 'bg-green-500/10 text-green-500'
                          : 'bg-muted text-muted-foreground'
                      }`}
                    >
                      {workflow.isEnabled ? 'Enabled' : 'Disabled'}
                    </span>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3">
                  {workflow.description && (
                    <p className="text-sm text-muted-foreground line-clamp-2">
                      {workflow.description}
                    </p>
                  )}

                  <div className="text-sm">
                    <span className="text-muted-foreground">Steps: </span>
                    <span className="font-medium">{workflow.steps.length}</span>
                  </div>

                  {workflow.tags && workflow.tags.length > 0 && (
                    <div className="flex flex-wrap gap-1">
                      {workflow.tags.slice(0, 3).map((tag) => (
                        <span
                          key={tag}
                          className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs"
                        >
                          {tag}
                        </span>
                      ))}
                      {workflow.tags.length > 3 && (
                        <span className="text-xs text-muted-foreground">
                          +{workflow.tags.length - 3} more
                        </span>
                      )}
                    </div>
                  )}

                  <div className="flex items-center justify-between pt-2 border-t">
                    <Link
                      to="/workflows/$workflowId"
                      params={{ workflowId: workflow.id }}
                      className="text-sm text-primary hover:underline inline-flex items-center gap-1"
                    >
                      View
                      <ExternalLink className="h-3 w-3" />
                    </Link>
                    <Link to="/workflows/$workflowId/run" params={{ workflowId: workflow.id }}>
                      <Button size="sm" variant="outline">
                        <Play className="h-3 w-3" />
                        Run
                      </Button>
                    </Link>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Recent Executions */}
      <div>
        <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
          <Clock className="h-5 w-5" />
          Recent Executions
        </h3>
        {!recentInstances || recentInstances.length === 0 ? (
          <Card>
            <CardContent>
              <EmptyState
                icon={GitBranch}
                title="No recent executions"
                description="Workflow executions will appear here"
              />
            </CardContent>
          </Card>
        ) : (
          <Card>
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                      Workflow
                    </th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                      Status
                    </th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                      Trigger
                    </th>
                    <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                      Started
                    </th>
                    <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {recentInstances.map((instance) => (
                    <tr
                      key={instance.id}
                      className="border-b last:border-0 hover:bg-muted/50 transition-colors"
                    >
                      <td className="px-4 py-3">
                        <div>
                          <Link
                            to="/workflows/$workflowId"
                            params={{ workflowId: instance.workflowId }}
                            className="font-medium hover:underline"
                          >
                            {instance.workflowId}
                          </Link>
                          <p className="text-xs text-muted-foreground font-mono">
                            {instance.id.slice(0, 8)}...
                          </p>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <WorkflowStatusBadge status={instance.status} />
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm">{instance.triggerType || 'Manual'}</span>
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm text-muted-foreground">
                          {formatDate(instance.createdAt)}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Link
                          to="/workflows/$workflowId/instances/$instanceId"
                          params={{
                            workflowId: instance.workflowId,
                            instanceId: instance.id,
                          }}
                          className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                        >
                          View
                          <ExternalLink className="h-3 w-3" />
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        )}
      </div>
    </div>
  )
}
