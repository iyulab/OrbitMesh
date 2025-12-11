import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, RefreshCw } from 'lucide-react'
import { WorkflowEditor, useWorkflowEditorStore } from '@/components/workflow-editor'
import { getWorkflow, updateWorkflow } from '@/api/client'
import { Button } from '@/components/ui/button'
import { toast } from '@/components/ui/sonner'
import type { WorkflowNode, WorkflowEdge } from '@/components/workflow-editor/types'
import { NODE_DEFINITIONS } from '@/components/workflow-editor/node-definitions'

export default function WorkflowEdit() {
  const { workflowId } = useParams<{ workflowId: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const clearWorkflow = useWorkflowEditorStore((state) => state.clearWorkflow)
  const loadWorkflow = useWorkflowEditorStore((state) => state.loadWorkflow)
  const [isLoaded, setIsLoaded] = useState(false)

  const { data: workflow, isLoading, error } = useQuery({
    queryKey: ['workflow', workflowId],
    queryFn: () => getWorkflow(workflowId!),
    enabled: !!workflowId,
  })

  // Load workflow into editor when data is available
  useEffect(() => {
    if (workflow && !isLoaded) {
      // Convert API workflow steps to editor nodes
      const nodes: WorkflowNode[] = workflow.steps.map((step, index) => {
        // Find the matching node type
        const nodeType = Object.keys(NODE_DEFINITIONS).find((key) => {
          const def = NODE_DEFINITIONS[key as keyof typeof NODE_DEFINITIONS]
          return key.includes(step.type) || def.label.toLowerCase().includes(step.type.toLowerCase())
        }) || 'action-execute'

        const definition = NODE_DEFINITIONS[nodeType as keyof typeof NODE_DEFINITIONS]

        return {
          id: step.id,
          type: 'workflowNode',
          position: { x: 100 + (index % 3) * 250, y: 100 + Math.floor(index / 3) * 150 },
          data: {
            label: step.name || step.type,
            description: definition?.description || '',
            nodeType: nodeType as keyof typeof NODE_DEFINITIONS,
            category: definition?.category || 'action',
            icon: definition?.icon || 'Zap',
            config: step.config,
            isConfigured: true,
          },
        }
      })

      // Create edges connecting sequential nodes
      const edges: WorkflowEdge[] = nodes.slice(0, -1).map((node, index) => ({
        id: `edge_${node.id}_${nodes[index + 1].id}`,
        source: node.id,
        target: nodes[index + 1].id,
        type: 'smoothstep',
        animated: true,
      }))

      loadWorkflow(nodes, edges, workflow.name, workflow.description || '')
      setIsLoaded(true)
    }
  }, [workflow, loadWorkflow, isLoaded])

  const updateMutation = useMutation({
    mutationFn: (data: Parameters<typeof updateWorkflow>[1]) =>
      updateWorkflow(workflowId!, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      queryClient.invalidateQueries({ queryKey: ['workflow', workflowId] })
      toast.success('Workflow updated successfully')
      clearWorkflow()
      navigate('/workflows')
    },
    onError: (error) => {
      toast.error('Failed to update workflow', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const handleSave = useCallback(
    (workflowData: { nodes: unknown[]; edges: unknown[]; name: string; description: string }) => {
      // Convert visual workflow to API format
      const steps = (workflowData.nodes as Array<{
        id: string
        data: {
          label: string
          nodeType: string
          config: Record<string, unknown>
        }
      }>)
        .filter((node) => node.data.nodeType !== 'trigger-manual' &&
                         node.data.nodeType !== 'trigger-schedule' &&
                         node.data.nodeType !== 'trigger-webhook')
        .map((node) => ({
          id: node.id,
          type: node.data.nodeType.replace('action-', '').replace('logic-', '').replace('transform-', ''),
          name: node.data.label,
          config: node.data.config,
        }))

      // Increment version
      const currentVersion = workflow?.version || '1.0.0'
      const versionParts = currentVersion.split('.')
      const newVersion = `${versionParts[0]}.${versionParts[1]}.${parseInt(versionParts[2] || '0') + 1}`

      updateMutation.mutate({
        name: workflowData.name,
        description: workflowData.description || undefined,
        version: newVersion,
        isActive: workflow?.isActive ?? true,
        steps,
      })
    },
    [updateMutation, workflow]
  )

  const handleBack = () => {
    clearWorkflow()
    navigate('/workflows')
  }

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-background">
        <RefreshCw className="w-8 h-8 text-slate-400 animate-spin" />
      </div>
    )
  }

  if (error || !workflow) {
    return (
      <div className="h-screen flex flex-col items-center justify-center bg-background">
        <h2 className="text-xl font-semibold text-slate-900 dark:text-white mb-2">
          Workflow not found
        </h2>
        <p className="text-slate-500 dark:text-slate-400 mb-4">
          The requested workflow could not be found.
        </p>
        <Button onClick={() => navigate('/workflows')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to Workflows
        </Button>
      </div>
    )
  }

  return (
    <div className="h-screen flex flex-col bg-background">
      {/* Header */}
      <div className="flex items-center gap-4 px-4 py-3 border-b bg-slate-50 dark:bg-slate-900">
        <Button variant="ghost" size="sm" onClick={handleBack}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to Workflows
        </Button>
        <div className="h-6 w-px bg-border" />
        <h1 className="text-lg font-semibold text-slate-900 dark:text-white">
          Edit Workflow
        </h1>
        <span className="text-sm text-slate-500 dark:text-slate-400">
          v{workflow.version}
        </span>
        {updateMutation.isPending && (
          <span className="text-sm text-muted-foreground ml-auto">Saving...</span>
        )}
      </div>

      {/* Editor */}
      <div className="flex-1 overflow-hidden">
        {isLoaded && (
          <WorkflowEditor onSave={handleSave} className="h-full" />
        )}
      </div>
    </div>
  )
}
