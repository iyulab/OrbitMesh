import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createWorkflow } from '@/lib/api'
import { useState } from 'react'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Button,
  Input,
  Textarea,
} from '@/components/ui'
import { ArrowLeft, Trash2, GripVertical, GitBranch, Save } from 'lucide-react'
import { toast } from 'sonner'
import type { WorkflowStep, StepType, StepConfig } from '@/types/workflow'

export const Route = createFileRoute('/workflows/new')({
  component: NewWorkflowPage,
})

const stepTypes: { value: StepType; label: string; description: string }[] = [
  { value: 'Job', label: 'Job', description: 'Execute a command on an agent' },
  { value: 'Parallel', label: 'Parallel', description: 'Run multiple steps in parallel' },
  { value: 'Conditional', label: 'Conditional', description: 'Branch based on condition' },
  { value: 'Delay', label: 'Delay', description: 'Wait for a duration' },
  { value: 'SubWorkflow', label: 'Sub-Workflow', description: 'Execute another workflow' },
  { value: 'Transform', label: 'Transform', description: 'Transform data between steps' },
  { value: 'Notify', label: 'Notify', description: 'Send notification' },
  { value: 'Approval', label: 'Approval', description: 'Wait for human approval' },
  { value: 'Log', label: 'Log', description: 'Log a message' },
]

function generateId() {
  return `step-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`
}

function NewWorkflowPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [version, setVersion] = useState('1.0.0')
  const [tags, setTags] = useState('')
  const [steps, setSteps] = useState<WorkflowStep[]>([])

  const createMutation = useMutation({
    mutationFn: createWorkflow,
    onSuccess: (workflow) => {
      queryClient.invalidateQueries({ queryKey: ['workflows'] })
      toast.success('Workflow created successfully')
      navigate({ to: '/workflows/$workflowId', params: { workflowId: workflow.id } })
    },
    onError: (error) => {
      toast.error(
        `Failed to create workflow: ${error instanceof Error ? error.message : 'Unknown error'}`
      )
    },
  })

  const addStep = (type: StepType) => {
    const newStep: WorkflowStep = {
      id: generateId(),
      name: `New ${type} Step`,
      type,
      config: getDefaultConfig(type),
    }
    setSteps([...steps, newStep])
  }

  const updateStep = (index: number, updates: Partial<WorkflowStep>) => {
    setSteps(steps.map((step, i) => (i === index ? { ...step, ...updates } : step)))
  }

  const removeStep = (index: number) => {
    setSteps(steps.filter((_, i) => i !== index))
  }

  const moveStep = (fromIndex: number, toIndex: number) => {
    const newSteps = [...steps]
    const [removed] = newSteps.splice(fromIndex, 1)
    newSteps.splice(toIndex, 0, removed)
    setSteps(newSteps)
  }

  const handleSave = () => {
    if (!name.trim()) {
      toast.error('Workflow name is required')
      return
    }

    if (steps.length === 0) {
      toast.error('At least one step is required')
      return
    }

    const workflowId = name.toLowerCase().replace(/\s+/g, '-')

    createMutation.mutate({
      id: workflowId,
      name: name.trim(),
      description: description.trim() || undefined,
      version,
      steps,
      tags: tags
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean),
      isEnabled: true,
    })
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Link
            to="/workflows"
            className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="h-4 w-4" />
            Back
          </Link>
          <div>
            <h2 className="text-2xl font-bold tracking-tight">New Workflow</h2>
            <p className="text-muted-foreground">Design your workflow step by step</p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={createMutation.isPending}>
          <Save className="h-4 w-4" />
          {createMutation.isPending ? 'Saving...' : 'Save Workflow'}
        </Button>
      </div>

      <div className="flex flex-col lg:flex-row gap-6">
        {/* Left Panel - Workflow Properties & Node Palette */}
        <div className="w-full lg:w-80 xl:w-96 flex-shrink-0 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Workflow Properties</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">Name *</label>
                <Input
                  placeholder="My Workflow"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Description</label>
                <Textarea
                  placeholder="What does this workflow do?"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={3}
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Version</label>
                <Input value={version} onChange={(e) => setVersion(e.target.value)} />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Tags</label>
                <Input
                  placeholder="tag1, tag2, tag3"
                  value={tags}
                  onChange={(e) => setTags(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">Comma-separated</p>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Add Step</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex flex-col gap-2">
                {stepTypes.map((type) => (
                  <button
                    key={type.value}
                    onClick={() => addStep(type.value)}
                    className="flex flex-col items-start p-3 rounded-lg border hover:border-primary hover:bg-primary/5 transition-colors text-left"
                  >
                    <span className="text-sm font-medium">{type.label}</span>
                    <span className="text-xs text-muted-foreground">
                      {type.description}
                    </span>
                  </button>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Right Panel - Steps Designer */}
        <div className="flex-1 min-w-0">
          <Card className="h-full">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <GitBranch className="h-4 w-4" />
                Steps ({steps.length})
              </CardTitle>
            </CardHeader>
            <CardContent>
              {steps.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 text-center">
                  <GitBranch className="h-12 w-12 text-muted-foreground mb-4" />
                  <p className="text-lg font-medium">No steps yet</p>
                  <p className="text-sm text-muted-foreground mt-1">
                    Add steps from the panel on the left
                  </p>
                </div>
              ) : (
                <div className="space-y-4">
                  {steps.map((step, index) => (
                    <StepEditor
                      key={step.id}
                      step={step}
                      index={index}
                      totalSteps={steps.length}
                      onUpdate={(updates) => updateStep(index, updates)}
                      onRemove={() => removeStep(index)}
                      onMoveUp={() => index > 0 && moveStep(index, index - 1)}
                      onMoveDown={() => index < steps.length - 1 && moveStep(index, index + 1)}
                      allSteps={steps}
                    />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}

interface StepEditorProps {
  step: WorkflowStep
  index: number
  totalSteps: number
  onUpdate: (updates: Partial<WorkflowStep>) => void
  onRemove: () => void
  onMoveUp: () => void
  onMoveDown: () => void
  allSteps: WorkflowStep[]
}

function StepEditor({
  step,
  index,
  totalSteps,
  onUpdate,
  onRemove,
  onMoveUp,
  onMoveDown,
  allSteps,
}: StepEditorProps) {
  const [expanded, setExpanded] = useState(true)

  const availableDependencies = allSteps.filter((s) => s.id !== step.id).map((s) => s.id)

  return (
    <div className="border rounded-lg overflow-hidden">
      <div
        className="flex items-center gap-3 p-3 bg-muted/50 cursor-pointer"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2 text-muted-foreground">
          <GripVertical className="h-4 w-4" />
          <span className="font-mono text-sm">{index + 1}</span>
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-medium truncate">{step.name}</span>
            <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
              {step.type}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={(e) => {
              e.stopPropagation()
              onMoveUp()
            }}
            disabled={index === 0}
            className="p-1 rounded hover:bg-muted disabled:opacity-30"
          >
            ↑
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation()
              onMoveDown()
            }}
            disabled={index === totalSteps - 1}
            className="p-1 rounded hover:bg-muted disabled:opacity-30"
          >
            ↓
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation()
              onRemove()
            }}
            className="p-1 rounded hover:bg-destructive/10 text-destructive"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      </div>

      {expanded && (
        <div className="p-4 space-y-4 border-t">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <label className="text-sm font-medium">Step Name</label>
              <Input
                value={step.name}
                onChange={(e) => onUpdate({ name: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Step ID</label>
              <Input
                value={step.id}
                onChange={(e) => onUpdate({ id: e.target.value })}
                className="font-mono"
              />
            </div>
          </div>

          {/* Step-type specific config */}
          <StepConfigEditor step={step} onUpdate={onUpdate} />

          {/* Dependencies */}
          {availableDependencies.length > 0 && (
            <div className="space-y-2">
              <label className="text-sm font-medium">Depends On</label>
              <div className="flex flex-wrap gap-2">
                {availableDependencies.map((depId) => {
                  const isSelected = step.dependsOn?.includes(depId)
                  return (
                    <button
                      key={depId}
                      onClick={() => {
                        const current = step.dependsOn || []
                        onUpdate({
                          dependsOn: isSelected
                            ? current.filter((d) => d !== depId)
                            : [...current, depId],
                        })
                      }}
                      className={`px-2 py-1 rounded text-xs font-mono ${
                        isSelected
                          ? 'bg-primary text-primary-foreground'
                          : 'bg-muted hover:bg-muted/80'
                      }`}
                    >
                      {depId}
                    </button>
                  )
                })}
              </div>
            </div>
          )}

          {/* Advanced Options */}
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Max Retries</label>
              <Input
                type="number"
                min="0"
                value={step.maxRetries || 0}
                onChange={(e) => onUpdate({ maxRetries: parseInt(e.target.value) || 0 })}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Timeout</label>
              <Input
                placeholder="e.g., 00:05:00"
                value={step.timeout || ''}
                onChange={(e) => onUpdate({ timeout: e.target.value || undefined })}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Output Variable</label>
              <Input
                placeholder="result"
                value={step.outputVariable || ''}
                onChange={(e) => onUpdate({ outputVariable: e.target.value || undefined })}
              />
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Condition</label>
            <Input
              placeholder="e.g., ${previousStep.status == 'success'}"
              value={step.condition || ''}
              onChange={(e) => onUpdate({ condition: e.target.value || undefined })}
            />
            <p className="text-xs text-muted-foreground">
              Expression that must be true for this step to execute
            </p>
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id={`continue-${step.id}`}
              checked={step.continueOnError || false}
              onChange={(e) => onUpdate({ continueOnError: e.target.checked })}
              className="rounded"
            />
            <label htmlFor={`continue-${step.id}`} className="text-sm">
              Continue on error
            </label>
          </div>
        </div>
      )}
    </div>
  )
}

interface StepConfigEditorProps {
  step: WorkflowStep
  onUpdate: (updates: Partial<WorkflowStep>) => void
}

function StepConfigEditor({ step, onUpdate }: StepConfigEditorProps) {
  const updateConfig = (configUpdates: Partial<StepConfig>) => {
    onUpdate({ config: { ...step.config, ...configUpdates } })
  }

  switch (step.type) {
    case 'Job':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Command *</label>
            <Input
              placeholder="e.g., ProcessData"
              value={step.config.command || ''}
              onChange={(e) => updateConfig({ command: e.target.value })}
            />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <label className="text-sm font-medium">Target Agent ID</label>
              <Input
                placeholder="Specific agent"
                value={(step.config.agentId as string) || ''}
                onChange={(e) => updateConfig({ agentId: e.target.value || undefined })}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Agent Group</label>
              <Input
                placeholder="e.g., workers"
                value={(step.config.agentGroup as string) || ''}
                onChange={(e) => updateConfig({ agentGroup: e.target.value || undefined })}
              />
            </div>
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">Payload (JSON)</label>
            <Textarea
              placeholder='{"key": "value"}'
              value={
                step.config.payload ? JSON.stringify(step.config.payload, null, 2) : ''
              }
              onChange={(e) => {
                try {
                  const parsed = e.target.value ? JSON.parse(e.target.value) : undefined
                  updateConfig({ payload: parsed })
                } catch {
                  // Invalid JSON, keep as string for now
                }
              }}
              rows={3}
              className="font-mono text-sm"
            />
          </div>
        </div>
      )

    case 'Delay':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Duration *</label>
            <Input
              placeholder="e.g., 00:00:30 (30 seconds)"
              value={(step.config.duration as string) || ''}
              onChange={(e) => updateConfig({ duration: e.target.value })}
            />
            <p className="text-xs text-muted-foreground">Format: HH:MM:SS</p>
          </div>
        </div>
      )

    case 'SubWorkflow':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Workflow ID *</label>
            <Input
              placeholder="workflow-to-call"
              value={(step.config.workflowId as string) || ''}
              onChange={(e) => updateConfig({ workflowId: e.target.value })}
            />
          </div>
        </div>
      )

    case 'Log':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Message *</label>
            <Input
              placeholder="Log message with ${variables}"
              value={(step.config.message as string) || ''}
              onChange={(e) => updateConfig({ message: e.target.value })}
            />
          </div>
        </div>
      )

    case 'Notify':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Channel *</label>
            <Input
              placeholder="e.g., email, slack, webhook"
              value={(step.config.channel as string) || ''}
              onChange={(e) => updateConfig({ channel: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">Message *</label>
            <Textarea
              placeholder="Notification message"
              value={(step.config.message as string) || ''}
              onChange={(e) => updateConfig({ message: e.target.value })}
              rows={2}
            />
          </div>
        </div>
      )

    case 'Transform':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Expression *</label>
            <Textarea
              placeholder="${input.field} + 1"
              value={(step.config.expression as string) || ''}
              onChange={(e) => updateConfig({ expression: e.target.value })}
              rows={2}
              className="font-mono text-sm"
            />
          </div>
        </div>
      )

    case 'Approval':
      return (
        <div className="space-y-4 p-3 bg-muted/30 rounded-lg">
          <div className="space-y-2">
            <label className="text-sm font-medium">Approvers</label>
            <Input
              placeholder="user1, user2"
              value={((step.config.approvers as string[]) || []).join(', ')}
              onChange={(e) =>
                updateConfig({
                  approvers: e.target.value
                    .split(',')
                    .map((s) => s.trim())
                    .filter(Boolean),
                })
              }
            />
            <p className="text-xs text-muted-foreground">Comma-separated list of approvers</p>
          </div>
        </div>
      )

    default:
      return (
        <div className="p-3 bg-muted/30 rounded-lg">
          <p className="text-sm text-muted-foreground">
            Configuration for {step.type} steps
          </p>
        </div>
      )
  }
}

function getDefaultConfig(type: StepType): StepConfig {
  switch (type) {
    case 'Job':
      return { command: '' }
    case 'Delay':
      return { duration: '00:00:30' }
    case 'Log':
      return { message: '' }
    case 'SubWorkflow':
      return { workflowId: '' }
    case 'Transform':
      return { expression: '' }
    case 'Notify':
      return { channel: '', message: '' }
    case 'Approval':
      return { approvers: [] }
    default:
      return {}
  }
}
