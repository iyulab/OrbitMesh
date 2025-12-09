import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  GitBranch,
  RefreshCw,
  Play,
  Clock,
  CheckCircle,
  XCircle,
  Plus,
  Trash2,
  ChevronDown,
  ChevronUp,
} from 'lucide-react'
import { getWorkflows, getWorkflowInstances, startWorkflow, createWorkflow } from '@/api/client'
import type { Workflow, WorkflowInstance, WorkflowInstanceStatus } from '@/types'

function WorkflowStatusBadge({ status }: { status: WorkflowInstanceStatus }) {
  const config: Record<WorkflowInstanceStatus, { class: string; icon: React.ComponentType<{ className?: string }> }> = {
    Running: { class: 'bg-yellow-500/20 text-yellow-600 dark:text-yellow-400', icon: Clock },
    Completed: { class: 'bg-green-500/20 text-green-600 dark:text-green-400', icon: CheckCircle },
    Failed: { class: 'bg-red-500/20 text-red-600 dark:text-red-400', icon: XCircle },
    Paused: { class: 'bg-blue-500/20 text-blue-600 dark:text-blue-400', icon: Clock },
    Cancelled: { class: 'bg-slate-500/20 text-slate-600 dark:text-slate-400', icon: XCircle },
  }

  const { class: className, icon: Icon } = config[status] || config.Running

  return (
    <span className={`status-badge flex items-center gap-1 ${className}`}>
      <Icon className="w-3 h-3" />
      {status}
    </span>
  )
}

interface WorkflowStep {
  id: string
  type: string
  name: string
  config: Record<string, unknown>
}

function CreateWorkflowModal({
  isOpen,
  onClose,
}: {
  isOpen: boolean
  onClose: () => void
}) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [steps, setSteps] = useState<WorkflowStep[]>([])
  const [showAddStep, setShowAddStep] = useState(false)
  const [newStep, setNewStep] = useState<WorkflowStep>({
    id: '',
    type: 'job',
    name: '',
    config: { command: '' },
  })

  const queryClient = useQueryClient()

  const createMutation = useMutation({
    mutationFn: createWorkflow,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      handleClose()
    },
  })

  const stepTypes = [
    { value: 'job', label: 'Job', description: 'Execute a command on an agent' },
    { value: 'delay', label: 'Delay', description: 'Wait for a specified duration' },
    { value: 'condition', label: 'Condition', description: 'Branch based on a condition' },
    { value: 'parallel', label: 'Parallel', description: 'Execute steps in parallel' },
  ]

  const handleAddStep = () => {
    if (!newStep.id || !newStep.name) return
    setSteps([...steps, { ...newStep }])
    setNewStep({ id: '', type: 'job', name: '', config: { command: '' } })
    setShowAddStep(false)
  }

  const handleRemoveStep = (index: number) => {
    setSteps(steps.filter((_, i) => i !== index))
  }

  const handleCreate = () => {
    if (!name.trim() || steps.length === 0) return
    createMutation.mutate({
      name,
      description: description || undefined,
      version: '1.0.0',
      steps,
    })
  }

  const handleClose = () => {
    setName('')
    setDescription('')
    setSteps([])
    setNewStep({ id: '', type: 'job', name: '', config: { command: '' } })
    setShowAddStep(false)
    onClose()
  }

  if (!isOpen) return null

  return (
    <div className="modal-backdrop">
      <div className="modal-content w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-slate-200 dark:border-slate-700">
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Create Workflow</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            Define a multi-step workflow to orchestrate agent tasks
          </p>
        </div>

        <div className="p-6 space-y-6">
          {/* Basic Info */}
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                Workflow Name *
              </label>
              <input
                type="text"
                className="input w-full"
                placeholder="my-workflow"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                Description
              </label>
              <textarea
                className="input w-full h-20"
                placeholder="Describe what this workflow does..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>
          </div>

          {/* Steps */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">
                Steps ({steps.length})
              </label>
              <button
                onClick={() => setShowAddStep(!showAddStep)}
                className="btn-secondary text-sm flex items-center gap-1"
              >
                {showAddStep ? <ChevronUp className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
                Add Step
              </button>
            </div>

            {/* Add Step Form */}
            {showAddStep && (
              <div className="p-4 bg-slate-100 dark:bg-slate-900 rounded-lg mb-4 space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-medium text-slate-600 dark:text-slate-400 mb-1">
                      Step ID *
                    </label>
                    <input
                      type="text"
                      className="input w-full text-sm"
                      placeholder="step-1"
                      value={newStep.id}
                      onChange={(e) => setNewStep({ ...newStep, id: e.target.value })}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-600 dark:text-slate-400 mb-1">
                      Step Name *
                    </label>
                    <input
                      type="text"
                      className="input w-full text-sm"
                      placeholder="Process Data"
                      value={newStep.name}
                      onChange={(e) => setNewStep({ ...newStep, name: e.target.value })}
                    />
                  </div>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 dark:text-slate-400 mb-1">
                    Step Type
                  </label>
                  <select
                    className="input w-full text-sm"
                    value={newStep.type}
                    onChange={(e) => setNewStep({ ...newStep, type: e.target.value })}
                  >
                    {stepTypes.map((type) => (
                      <option key={type.value} value={type.value}>
                        {type.label} - {type.description}
                      </option>
                    ))}
                  </select>
                </div>
                {newStep.type === 'job' && (
                  <div>
                    <label className="block text-xs font-medium text-slate-600 dark:text-slate-400 mb-1">
                      Command
                    </label>
                    <input
                      type="text"
                      className="input w-full text-sm"
                      placeholder="process-data"
                      value={(newStep.config.command as string) || ''}
                      onChange={(e) => setNewStep({
                        ...newStep,
                        config: { ...newStep.config, command: e.target.value }
                      })}
                    />
                  </div>
                )}
                {newStep.type === 'delay' && (
                  <div>
                    <label className="block text-xs font-medium text-slate-600 dark:text-slate-400 mb-1">
                      Duration (e.g., 30s, 5m)
                    </label>
                    <input
                      type="text"
                      className="input w-full text-sm"
                      placeholder="30s"
                      value={(newStep.config.duration as string) || ''}
                      onChange={(e) => setNewStep({
                        ...newStep,
                        config: { ...newStep.config, duration: e.target.value }
                      })}
                    />
                  </div>
                )}
                <button
                  onClick={handleAddStep}
                  disabled={!newStep.id || !newStep.name}
                  className="btn-primary text-sm w-full"
                >
                  Add Step
                </button>
              </div>
            )}

            {/* Steps List */}
            {steps.length === 0 ? (
              <div className="text-center py-8 bg-slate-50 dark:bg-slate-900 rounded-lg border-2 border-dashed border-slate-200 dark:border-slate-700">
                <GitBranch className="w-8 h-8 text-slate-400 mx-auto mb-2" />
                <p className="text-sm text-slate-500 dark:text-slate-400">
                  No steps defined yet. Add steps to build your workflow.
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {steps.map((step, index) => (
                  <div
                    key={step.id}
                    className="flex items-center gap-3 p-3 bg-slate-50 dark:bg-slate-900 rounded-lg"
                  >
                    <div className="w-6 h-6 rounded-full bg-orbit-600 text-white text-xs flex items-center justify-center font-medium">
                      {index + 1}
                    </div>
                    <div className="flex-1">
                      <p className="text-sm font-medium text-slate-900 dark:text-white">{step.name}</p>
                      <p className="text-xs text-slate-500 dark:text-slate-400">
                        {step.type} • {step.id}
                      </p>
                    </div>
                    <button
                      onClick={() => handleRemoveStep(index)}
                      className="p-1 text-slate-400 hover:text-red-500"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="p-6 border-t border-slate-200 dark:border-slate-700 flex justify-end gap-3">
          <button onClick={handleClose} className="btn-secondary">
            Cancel
          </button>
          <button
            onClick={handleCreate}
            disabled={!name.trim() || steps.length === 0 || createMutation.isPending}
            className="btn-primary"
          >
            {createMutation.isPending ? 'Creating...' : 'Create Workflow'}
          </button>
        </div>
      </div>
    </div>
  )
}

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
      handleClose()
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
      alert('Invalid JSON input')
      return
    }
    startMutation.mutate({ workflowId: workflow.id, input })
  }

  const handleClose = () => {
    setInputJson('{}')
    onClose()
  }

  if (!isOpen || !workflow) return null

  return (
    <div className="modal-backdrop">
      <div className="modal-content w-full max-w-md">
        <div className="p-6 border-b border-slate-200 dark:border-slate-700">
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Start Workflow</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            Run "{workflow.name}"
          </p>
        </div>

        <div className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              Input Variables (JSON)
            </label>
            <textarea
              className="input w-full h-32 font-mono text-sm"
              placeholder='{"key": "value"}'
              value={inputJson}
              onChange={(e) => setInputJson(e.target.value)}
            />
            <p className="text-xs text-slate-500 mt-1">
              Optional initial variables for the workflow
            </p>
          </div>
        </div>

        <div className="p-6 border-t border-slate-200 dark:border-slate-700 flex justify-end gap-3">
          <button onClick={handleClose} className="btn-secondary">
            Cancel
          </button>
          <button
            onClick={handleStart}
            disabled={startMutation.isPending}
            className="btn-primary flex items-center gap-2"
          >
            <Play className="w-4 h-4" />
            {startMutation.isPending ? 'Starting...' : 'Start'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function Workflows() {
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showStartModal, setShowStartModal] = useState(false)
  const [selectedWorkflow, setSelectedWorkflow] = useState<Workflow | null>(null)
  const [expandedWorkflow, setExpandedWorkflow] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const { data: workflows = [], isLoading: loadingWorkflows } = useQuery({
    queryKey: ['workflows'],
    queryFn: getWorkflows,
  })

  const { data: instances = [], isLoading: loadingInstances } = useQuery({
    queryKey: ['workflow-instances'],
    queryFn: () => getWorkflowInstances(),
  })

  const isLoading = loadingWorkflows || loadingInstances

  const handleStartWorkflow = (workflow: Workflow) => {
    setSelectedWorkflow(workflow)
    setShowStartModal(true)
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Workflows</h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Orchestrate complex multi-step processes</p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={() => {
              queryClient.invalidateQueries({ queryKey: ['workflows'] })
              queryClient.invalidateQueries({ queryKey: ['workflow-instances'] })
            }}
            className="btn-secondary flex items-center gap-2"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button
            onClick={() => setShowCreateModal(true)}
            className="btn-primary flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            Create Workflow
          </button>
        </div>
      </div>

      {/* Workflow Definitions */}
      <div className="card">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">Workflow Definitions</h2>
        {isLoading ? (
          <div className="text-center py-8">
            <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
            <p className="text-slate-500 dark:text-slate-400">Loading workflows...</p>
          </div>
        ) : workflows.length === 0 ? (
          <div className="text-center py-12">
            <GitBranch className="w-12 h-12 text-slate-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">No workflows defined</h3>
            <p className="text-slate-500 dark:text-slate-400 mb-4">
              Create your first workflow to orchestrate agent tasks
            </p>
            <button onClick={() => setShowCreateModal(true)} className="btn-primary">
              Create Workflow
            </button>
          </div>
        ) : (
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
                      <h3 className="text-slate-900 dark:text-white font-medium">{workflow.name}</h3>
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
                        <span className={`text-xs ${workflow.isActive ? 'text-green-600 dark:text-green-400' : 'text-slate-500 dark:text-slate-500'}`}>
                          {workflow.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <button
                      onClick={() => setExpandedWorkflow(
                        expandedWorkflow === workflow.id ? null : workflow.id
                      )}
                      className="btn-secondary text-sm flex items-center gap-1"
                    >
                      {expandedWorkflow === workflow.id ? (
                        <ChevronUp className="w-4 h-4" />
                      ) : (
                        <ChevronDown className="w-4 h-4" />
                      )}
                      Steps
                    </button>
                    <button
                      onClick={() => handleStartWorkflow(workflow)}
                      className="btn-primary text-sm flex items-center gap-1"
                    >
                      <Play className="w-4 h-4" />
                      Run
                    </button>
                  </div>
                </div>

                {/* Expanded Steps */}
                {expandedWorkflow === workflow.id && (
                  <div className="mt-4 pt-4 border-t border-slate-200 dark:border-slate-700">
                    <h4 className="text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">Workflow Steps</h4>
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
                            <p className="text-sm text-slate-900 dark:text-white">{step.name || step.id}</p>
                            <p className="text-xs text-slate-500 dark:text-slate-400">{step.type}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Recent Instances */}
      <div className="card">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">Recent Executions</h2>
        {instances.length === 0 ? (
          <p className="text-slate-500 dark:text-slate-500 text-center py-8">No workflow executions yet</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="table-header">
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Instance ID</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Workflow</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Status</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Started</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Completed</th>
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
                      <span className="text-slate-900 dark:text-white">{instance.workflowId}</span>
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

      {/* Modals */}
      <CreateWorkflowModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
      />
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
