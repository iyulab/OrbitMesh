import { useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { WorkflowEditor, useWorkflowEditorStore } from '@/components/workflow-editor'
import { createWorkflow } from '@/api/client'
import { Button } from '@/components/ui/button'
import { toast } from '@/components/ui/sonner'

export default function WorkflowCreate() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const clearWorkflow = useWorkflowEditorStore((state) => state.clearWorkflow)

  const createMutation = useMutation({
    mutationFn: createWorkflow,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      toast.success('Workflow created successfully')
      clearWorkflow()
      navigate('/workflows')
    },
    onError: (error) => {
      toast.error('Failed to create workflow', {
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

      createMutation.mutate({
        name: workflowData.name,
        description: workflowData.description || undefined,
        version: '1.0.0',
        steps,
      })
    },
    [createMutation]
  )

  const handleBack = () => {
    clearWorkflow()
    navigate('/workflows')
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
          Create New Workflow
        </h1>
        {createMutation.isPending && (
          <span className="text-sm text-muted-foreground ml-auto">Saving...</span>
        )}
        {createMutation.isError && (
          <span className="text-sm text-destructive ml-auto">
            Error: {createMutation.error instanceof Error ? createMutation.error.message : 'Failed to save'}
          </span>
        )}
      </div>

      {/* Editor */}
      <div className="flex-1 overflow-hidden">
        <WorkflowEditor onSave={handleSave} className="h-full" />
      </div>
    </div>
  )
}
