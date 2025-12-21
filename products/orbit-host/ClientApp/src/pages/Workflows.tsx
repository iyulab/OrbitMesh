import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  GitBranch,
  RefreshCw,
  Play,
  Plus,
  Pencil,
  List,
  LayoutGrid,
  ChevronDown,
  ChevronUp,
  Copy,
  Trash2,
  MoreVertical,
} from 'lucide-react'
import { getWorkflows, getWorkflowInstances, startWorkflow, duplicateWorkflow, deleteWorkflow } from '@/api/client'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { Workflow } from '@/types'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { WorkflowStatusBadge } from '@/components/ui/status-badge'
import { toast } from '@/components/ui/sonner'

function StartWorkflowModal({
  isOpen,
  onClose,
  workflow,
}: {
  isOpen: boolean
  onClose: () => void
  workflow: Workflow | null
}) {
  const [inputJson, setInputJson] = useState('{}')
  const queryClient = useQueryClient()

  const startMutation = useMutation({
    mutationFn: (data: { workflowId: string; input?: Record<string, unknown> }) =>
      startWorkflow(data.workflowId, data.input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
      toast.success('Workflow started successfully')
      handleClose()
    },
    onError: (error) => {
      toast.error('Failed to start workflow', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const handleStart = () => {
    if (!workflow) return
    let input: Record<string, unknown> | undefined
    try {
      const parsed = JSON.parse(inputJson)
      if (Object.keys(parsed).length > 0) {
        input = parsed
      }
    } catch {
      toast.error('Invalid JSON input', {
        description: 'Please enter valid JSON format',
      })
      return
    }
    startMutation.mutate({ workflowId: workflow.id, input })
  }

  const handleClose = () => {
    setInputJson('{}')
    onClose()
  }

  if (!workflow) return null

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Start Workflow</DialogTitle>
          <DialogDescription>Run "{workflow.name}"</DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="input-vars">Input Variables (JSON)</Label>
            <Textarea
              id="input-vars"
              className="font-mono text-sm h-32"
              placeholder='{"key": "value"}'
              value={inputJson}
              onChange={(e) => setInputJson(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Optional initial variables for the workflow
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            Cancel
          </Button>
          <Button onClick={handleStart} disabled={startMutation.isPending}>
            <Play className="w-4 h-4 mr-2" />
            {startMutation.isPending ? 'Starting...' : 'Start'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}


export default function Workflows() {
  const navigate = useNavigate()
  const [showStartModal, setShowStartModal] = useState(false)
  const [selectedWorkflow, setSelectedWorkflow] = useState<Workflow | null>(null)
  const [expandedWorkflow, setExpandedWorkflow] = useState<string | null>(null)
  const [viewMode, setViewMode] = useState<'list' | 'grid'>('list')
  const [isRefreshing, setIsRefreshing] = useState(false)
  const queryClient = useQueryClient()

  const { data: workflows = [], isLoading: loadingWorkflows } = useQuery({
    queryKey: ['workflows'],
    queryFn: getWorkflows,
  })

  const { data: instances = [], isLoading: loadingInstances } = useQuery({
    queryKey: ['workflow-instances'],
    queryFn: () => getWorkflowInstances(),
  })

  const handleRefresh = async () => {
    setIsRefreshing(true)
    try {
      const [workflowsData, instancesData] = await Promise.all([
        getWorkflows(),
        getWorkflowInstances(),
      ])
      queryClient.setQueryData(['workflows'], workflowsData)
      queryClient.setQueryData(['workflow-instances'], instancesData)
    } catch (error) {
      console.error('Failed to refresh workflows:', error)
    } finally {
      setIsRefreshing(false)
    }
  }

  const isFetching = loadingWorkflows || loadingInstances || isRefreshing

  const duplicateMutation = useMutation({
    mutationFn: (workflowId: string) => duplicateWorkflow(workflowId),
    onSuccess: (newWorkflow) => {
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      toast.success('Workflow duplicated successfully', {
        description: `Created "${newWorkflow.name}"`,
      })
    },
    onError: (error) => {
      toast.error('Failed to duplicate workflow', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (workflowId: string) => deleteWorkflow(workflowId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      toast.success('Workflow deleted successfully')
    },
    onError: (error) => {
      toast.error('Failed to delete workflow', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const isLoading = loadingWorkflows || loadingInstances

  const handleStartWorkflow = (workflow: Workflow) => {
    setSelectedWorkflow(workflow)
    setShowStartModal(true)
  }

  const handleEditWorkflow = (workflow: Workflow) => {
    navigate(`/workflows/${workflow.id}/edit`)
  }

  const handleCreateWorkflow = () => {
    navigate('/workflows/new')
  }

  const handleDuplicateWorkflow = (workflow: Workflow) => {
    duplicateMutation.mutate(workflow.id)
  }

  const handleDeleteWorkflow = (workflow: Workflow) => {
    if (confirm(`Are you sure you want to delete "${workflow.name}"? This action cannot be undone.`)) {
      deleteMutation.mutate(workflow.id)
    }
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Workflows</h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">
            Orchestrate complex multi-step processes with visual workflow builder
          </p>
        </div>
        <div className="flex gap-3">
          <div className="flex border rounded-lg overflow-hidden">
            <Button
              variant={viewMode === 'list' ? 'secondary' : 'ghost'}
              size="sm"
              onClick={() => setViewMode('list')}
              className="rounded-none"
            >
              <List className="w-4 h-4" />
            </Button>
            <Button
              variant={viewMode === 'grid' ? 'secondary' : 'ghost'}
              size="sm"
              onClick={() => setViewMode('grid')}
              className="rounded-none"
            >
              <LayoutGrid className="w-4 h-4" />
            </Button>
          </div>
          <Button
            variant="outline"
            onClick={handleRefresh}
            disabled={isFetching}
          >
            <RefreshCw className={`w-4 h-4 mr-2 ${isFetching ? 'animate-spin' : ''}`} />
            {isFetching ? 'Refreshing...' : 'Refresh'}
          </Button>
          <Button onClick={handleCreateWorkflow}>
            <Plus className="w-4 h-4 mr-2" />
            Create Workflow
          </Button>
        </div>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="definitions" className="space-y-4">
        <TabsList>
          <TabsTrigger value="definitions">Workflow Definitions</TabsTrigger>
          <TabsTrigger value="executions">Recent Executions</TabsTrigger>
        </TabsList>

        {/* Workflow Definitions Tab */}
        <TabsContent value="definitions">
          <div className="card">
            {isLoading ? (
              <div className="text-center py-8">
                <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
                <p className="text-slate-500 dark:text-slate-400">Loading workflows...</p>
              </div>
            ) : workflows.length === 0 ? (
              <div className="text-center py-12">
                <GitBranch className="w-12 h-12 text-slate-400 mx-auto mb-4" />
                <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">
                  No workflows defined
                </h3>
                <p className="text-slate-500 dark:text-slate-400 mb-4">
                  Create your first workflow using the visual workflow builder
                </p>
                <Button onClick={handleCreateWorkflow}>
                  <Plus className="w-4 h-4 mr-2" />
                  Create Workflow
                </Button>
              </div>
            ) : viewMode === 'list' ? (
              <div className="space-y-4">
                {workflows.map((workflow) => (
                  <div
                    key={workflow.id}
                    className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg border border-slate-200 dark:border-slate-700"
                  >
                    <div className="flex items-start justify-between">
                      <div className="flex items-start gap-3">
                        <div className="p-2 bg-orbit-100 dark:bg-orbit-600/20 rounded-lg">
                          <GitBranch className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
                        </div>
                        <div>
                          <h3 className="text-slate-900 dark:text-white font-medium">
                            {workflow.name}
                          </h3>
                          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                            {workflow.description || 'No description'}
                          </p>
                          <div className="flex items-center gap-4 mt-2">
                            <span className="text-xs text-slate-500 dark:text-slate-500">
                              Version: {workflow.version}
                            </span>
                            <span className="text-xs text-slate-500 dark:text-slate-500">
                              Steps: {workflow.steps.length}
                            </span>
                            <span
                              className={`text-xs ${workflow.isActive ? 'text-green-600 dark:text-green-400' : 'text-slate-500 dark:text-slate-500'}`}
                            >
                              {workflow.isActive ? 'Active' : 'Inactive'}
                            </span>
                          </div>
                        </div>
                      </div>
                      <div className="flex gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() =>
                            setExpandedWorkflow(
                              expandedWorkflow === workflow.id ? null : workflow.id
                            )
                          }
                        >
                          {expandedWorkflow === workflow.id ? (
                            <ChevronUp className="w-4 h-4 mr-1" />
                          ) : (
                            <ChevronDown className="w-4 h-4 mr-1" />
                          )}
                          Steps
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleEditWorkflow(workflow)}
                        >
                          <Pencil className="w-4 h-4 mr-1" />
                          Edit
                        </Button>
                        <Button size="sm" onClick={() => handleStartWorkflow(workflow)}>
                          <Play className="w-4 h-4 mr-1" />
                          Run
                        </Button>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="sm">
                              <MoreVertical className="w-4 h-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem onClick={() => handleDuplicateWorkflow(workflow)}>
                              <Copy className="w-4 h-4 mr-2" />
                              Duplicate
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              onClick={() => handleDeleteWorkflow(workflow)}
                              className="text-destructive focus:text-destructive"
                            >
                              <Trash2 className="w-4 h-4 mr-2" />
                              Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </div>

                    {/* Expanded Steps */}
                    {expandedWorkflow === workflow.id && (
                      <div className="mt-4 pt-4 border-t border-slate-200 dark:border-slate-700">
                        <h4 className="text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                          Workflow Steps
                        </h4>
                        <div className="space-y-2">
                          {workflow.steps.map((step, index) => (
                            <div
                              key={step.id}
                              className="flex items-center gap-3 p-2 bg-white dark:bg-slate-800 rounded border border-slate-200 dark:border-slate-600"
                            >
                              <div className="w-5 h-5 rounded-full bg-orbit-600 text-white text-xs flex items-center justify-center">
                                {index + 1}
                              </div>
                              <div className="flex-1">
                                <p className="text-sm text-slate-900 dark:text-white">
                                  {step.name || step.id}
                                </p>
                                <p className="text-xs text-slate-500 dark:text-slate-400">
                                  {step.type}
                                </p>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {workflows.map((workflow) => (
                  <div
                    key={workflow.id}
                    className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg border border-slate-200 dark:border-slate-700 hover:border-orbit-500 transition-colors"
                  >
                    <div className="flex items-center justify-between mb-3">
                      <div className="flex items-center gap-3">
                        <div className="p-2 bg-orbit-100 dark:bg-orbit-600/20 rounded-lg">
                          <GitBranch className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
                        </div>
                        <div>
                          <h3 className="text-slate-900 dark:text-white font-medium">
                            {workflow.name}
                          </h3>
                          <span
                            className={`text-xs ${workflow.isActive ? 'text-green-600 dark:text-green-400' : 'text-slate-500 dark:text-slate-500'}`}
                          >
                            {workflow.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </div>
                      </div>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="sm" className="-mr-2">
                            <MoreVertical className="w-4 h-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => handleDuplicateWorkflow(workflow)}>
                            <Copy className="w-4 h-4 mr-2" />
                            Duplicate
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            onClick={() => handleDeleteWorkflow(workflow)}
                            className="text-destructive focus:text-destructive"
                          >
                            <Trash2 className="w-4 h-4 mr-2" />
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                    <p className="text-sm text-slate-500 dark:text-slate-400 mb-3 line-clamp-2">
                      {workflow.description || 'No description'}
                    </p>
                    <div className="flex items-center justify-between text-xs text-slate-500 mb-3">
                      <span>v{workflow.version}</span>
                      <span>{workflow.steps.length} steps</span>
                    </div>
                    <div className="flex gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        className="flex-1"
                        onClick={() => handleEditWorkflow(workflow)}
                      >
                        <Pencil className="w-4 h-4 mr-1" />
                        Edit
                      </Button>
                      <Button size="sm" className="flex-1" onClick={() => handleStartWorkflow(workflow)}>
                        <Play className="w-4 h-4 mr-1" />
                        Run
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </TabsContent>

        {/* Executions Tab */}
        <TabsContent value="executions">
          <div className="card">
            {instances.length === 0 ? (
              <p className="text-slate-500 dark:text-slate-500 text-center py-8">
                No workflow executions yet
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="table-header">
                      <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">
                        Instance ID
                      </th>
                      <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">
                        Workflow
                      </th>
                      <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">
                        Status
                      </th>
                      <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">
                        Started
                      </th>
                      <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">
                        Completed
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {instances.map((instance) => (
                      <tr key={instance.id} className="table-row">
                        <td className="py-3 px-4">
                          <span className="text-xs text-slate-500 dark:text-slate-400 font-mono">
                            {instance.id.substring(0, 8)}...
                          </span>
                        </td>
                        <td className="py-3 px-4">
                          <span className="text-slate-900 dark:text-white">
                            {instance.workflowId}
                          </span>
                        </td>
                        <td className="py-3 px-4">
                          <WorkflowStatusBadge status={instance.status} />
                        </td>
                        <td className="py-3 px-4">
                          <span className="text-sm text-slate-500 dark:text-slate-400">
                            {new Date(instance.startedAt).toLocaleString()}
                          </span>
                        </td>
                        <td className="py-3 px-4">
                          <span className="text-sm text-slate-500 dark:text-slate-400">
                            {instance.completedAt
                              ? new Date(instance.completedAt).toLocaleString()
                              : '-'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </TabsContent>
      </Tabs>

      {/* Modals */}
      <StartWorkflowModal
        isOpen={showStartModal}
        onClose={() => {
          setShowStartModal(false)
          setSelectedWorkflow(null)
        }}
        workflow={selectedWorkflow}
      />
    </div>
  )
}
