import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getWorkflow, startWorkflow } from '@/lib/api'
import { useState } from 'react'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Button,
  Textarea,
  LoadingState,
  ErrorState,
} from '@/components/ui'
import { ArrowLeft, Play, GitBranch } from 'lucide-react'
import { toast } from 'sonner'

export const Route = createFileRoute('/workflows/$workflowId/run')({
  component: RunWorkflowPage,
})

function RunWorkflowPage() {
  const { workflowId } = Route.useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [inputJson, setInputJson] = useState('{}')
  const [jsonError, setJsonError] = useState<string | null>(null)

  const {
    data: workflow,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['workflow', workflowId],
    queryFn: () => getWorkflow(workflowId),
  })

  const startMutation = useMutation({
    mutationFn: (input: Record<string, unknown> | undefined) => startWorkflow(workflowId, input),
    onSuccess: (instance) => {
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
      toast.success('Workflow started successfully')
      navigate({
        to: '/workflows/$workflowId/instances/$instanceId',
        params: { workflowId, instanceId: instance.id },
      })
    },
    onError: (error) => {
      toast.error(
        `Failed to start workflow: ${error instanceof Error ? error.message : 'Unknown error'}`
      )
    },
  })

  const handleInputChange = (value: string) => {
    setInputJson(value)
    try {
      JSON.parse(value)
      setJsonError(null)
    } catch {
      setJsonError('Invalid JSON')
    }
  }

  const handleStart = () => {
    try {
      const input = JSON.parse(inputJson)
      const hasInput = Object.keys(input).length > 0
      startMutation.mutate(hasInput ? input : undefined)
    } catch {
      toast.error('Invalid JSON input')
    }
  }

  if (isLoading) {
    return <LoadingState text="Loading workflow..." />
  }

  if (error) {
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load workflow'}
        onRetry={refetch}
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

      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
            <Play className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h2 className="text-2xl font-bold">Run Workflow</h2>
            <p className="text-sm text-muted-foreground">{workflow.name}</p>
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Input Configuration */}
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Input Variables</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">Input (JSON)</label>
              <Textarea
                placeholder='{"key": "value"}'
                value={inputJson}
                onChange={(e) => handleInputChange(e.target.value)}
                rows={10}
                className={`font-mono text-sm ${jsonError ? 'border-destructive' : ''}`}
              />
              {jsonError && <p className="text-xs text-destructive">{jsonError}</p>}
              <p className="text-xs text-muted-foreground">
                Provide input variables as a JSON object. These will be available to all steps.
              </p>
            </div>

            <Button
              className="w-full"
              onClick={handleStart}
              disabled={startMutation.isPending || !!jsonError}
            >
              <Play className="h-4 w-4" />
              {startMutation.isPending ? 'Starting...' : 'Start Workflow'}
            </Button>
          </CardContent>
        </Card>

        {/* Workflow Info */}
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Workflow Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <DetailRow label="Name" value={workflow.name} />
              <DetailRow label="Version" value={workflow.version} />
              <DetailRow label="Steps" value={workflow.steps.length.toString()} />
              {workflow.description && (
                <div>
                  <p className="text-sm text-muted-foreground mb-1">Description</p>
                  <p className="text-sm">{workflow.description}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-sm">
                <GitBranch className="h-4 w-4" />
                Steps Overview
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-2">
                {workflow.steps.map((step, index) => (
                  <div
                    key={step.id}
                    className="flex items-center gap-3 p-2 rounded bg-muted/50"
                  >
                    <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary text-xs font-medium">
                      {index + 1}
                    </span>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium truncate">{step.name}</p>
                    </div>
                    <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
                      {step.type}
                    </span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
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
