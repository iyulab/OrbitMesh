import { useMemo, useCallback } from 'react'
import {
  Play,
  Clock,
  Webhook,
  Zap,
  Globe,
  Timer,
  GitBranch,
  GitMerge,
  Repeat,
  Filter,
  Shuffle,
  Layers,
  X,
  Copy,
  Trash2,
  Info,
} from 'lucide-react'
import { useWorkflowEditorStore } from './store'
import { NODE_DEFINITIONS } from './node-definitions'
import type { ConfigField, NodeCategory } from './types'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Switch } from '@/components/ui/switch'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { Separator } from '@/components/ui/separator'

// Icon mapping
const ICON_MAP: Record<string, React.ComponentType<{ className?: string }>> = {
  Play,
  Clock,
  Webhook,
  Zap,
  Globe,
  Timer,
  GitBranch,
  GitMerge,
  Repeat,
  Filter,
  Shuffle,
  Layers,
}

// Category colors
const CATEGORY_COLORS: Record<NodeCategory, string> = {
  trigger: '#22c55e',
  action: '#3b82f6',
  logic: '#f59e0b',
  transform: '#8b5cf6',
  integration: '#ec4899',
}

interface NodeConfigPanelProps {
  className?: string
}

export function NodeConfigPanel({ className }: NodeConfigPanelProps) {
  const selectedNodeId = useWorkflowEditorStore((state) => state.editor.selectedNodeId)
  const nodes = useWorkflowEditorStore((state) => state.nodes)
  const updateNodeConfig = useWorkflowEditorStore((state) => state.updateNodeConfig)
  const updateNodeData = useWorkflowEditorStore((state) => state.updateNodeData)
  const deleteNode = useWorkflowEditorStore((state) => state.deleteNode)
  const duplicateNode = useWorkflowEditorStore((state) => state.duplicateNode)
  const clearSelection = useWorkflowEditorStore((state) => state.clearSelection)

  // Find the selected node
  const selectedNode = useMemo(() => {
    if (!selectedNodeId) return null
    return nodes.find((n) => n.id === selectedNodeId) ?? null
  }, [selectedNodeId, nodes])

  // Get node definition
  const nodeDefinition = useMemo(() => {
    if (!selectedNode) return null
    return NODE_DEFINITIONS[selectedNode.data.nodeType] ?? null
  }, [selectedNode])

  // Handle config field change
  const handleConfigChange = useCallback(
    (key: string, value: unknown) => {
      if (!selectedNodeId) return
      updateNodeConfig(selectedNodeId, { [key]: value })
    },
    [selectedNodeId, updateNodeConfig]
  )

  // Handle label change
  const handleLabelChange = useCallback(
    (label: string) => {
      if (!selectedNodeId) return
      updateNodeData(selectedNodeId, { label })
    },
    [selectedNodeId, updateNodeData]
  )

  // No node selected
  if (!selectedNode || !nodeDefinition) {
    return (
      <div className={cn('flex flex-col h-full bg-background border-l', className)}>
        <div className="p-4 border-b">
          <h2 className="font-semibold">Configuration</h2>
        </div>
        <div className="flex-1 flex items-center justify-center p-8">
          <div className="text-center text-muted-foreground">
            <Info className="w-12 h-12 mx-auto mb-4 opacity-30" />
            <p className="text-sm">Select a node to configure its settings</p>
          </div>
        </div>
      </div>
    )
  }

  const Icon = ICON_MAP[nodeDefinition.icon] || Zap
  const categoryColor = CATEGORY_COLORS[selectedNode.data.category]

  return (
    <div className={cn('flex flex-col h-full bg-background border-l', className)}>
      {/* Header */}
      <div className="p-4 border-b">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <div
              className="w-8 h-8 rounded flex items-center justify-center"
              style={{ backgroundColor: categoryColor }}
            >
              <Icon className="w-4 h-4 text-white" />
            </div>
            <div>
              <h2 className="font-semibold text-sm">{nodeDefinition.label}</h2>
              <p className="text-xs text-muted-foreground capitalize">
                {selectedNode.data.category} node
              </p>
            </div>
          </div>
          <Button
            variant="ghost"
            size="icon"
            onClick={clearSelection}
            className="h-8 w-8"
          >
            <X className="w-4 h-4" />
          </Button>
        </div>

        {/* Quick Actions */}
        <div className="flex gap-2">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => selectedNodeId && duplicateNode(selectedNodeId)}
                  className="flex-1"
                >
                  <Copy className="w-4 h-4 mr-1" />
                  Duplicate
                </Button>
              </TooltipTrigger>
              <TooltipContent>Create a copy of this node</TooltipContent>
            </Tooltip>
          </TooltipProvider>

          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => selectedNodeId && deleteNode(selectedNodeId)}
                  className="flex-1 text-destructive hover:text-destructive"
                >
                  <Trash2 className="w-4 h-4 mr-1" />
                  Delete
                </Button>
              </TooltipTrigger>
              <TooltipContent>Remove this node</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>
      </div>

      {/* Configuration Fields */}
      <ScrollArea className="flex-1">
        <div className="p-4 space-y-4">
          {/* Node Label */}
          <div className="space-y-2">
            <Label htmlFor="node-label">Node Label</Label>
            <Input
              id="node-label"
              value={selectedNode.data.label}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => handleLabelChange(e.target.value)}
              placeholder="Enter node label"
            />
            <p className="text-xs text-muted-foreground">
              Customize how this node appears in the workflow
            </p>
          </div>

          <Separator />

          {/* Dynamic Config Fields */}
          {nodeDefinition.configSchema.map((field) => (
            <ConfigFieldInput
              key={field.key}
              field={field}
              value={selectedNode.data.config[field.key]}
              onChange={(value) => handleConfigChange(field.key, value)}
            />
          ))}

          {nodeDefinition.configSchema.length === 0 && (
            <div className="text-center py-4 text-muted-foreground">
              <p className="text-sm">This node has no configuration options</p>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Footer */}
      <div className="p-3 border-t text-xs text-muted-foreground bg-muted/30">
        <p>Node ID: {selectedNodeId}</p>
      </div>
    </div>
  )
}

interface ConfigFieldInputProps {
  field: ConfigField
  value: unknown
  onChange: (value: unknown) => void
}

function ConfigFieldInput({ field, value, onChange }: ConfigFieldInputProps) {
  const renderInput = () => {
    switch (field.type) {
      case 'text':
        return (
          <Input
            value={(value as string) ?? field.defaultValue ?? ''}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
            placeholder={field.placeholder}
          />
        )

      case 'textarea':
        return (
          <Textarea
            value={(value as string) ?? field.defaultValue ?? ''}
            onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => onChange(e.target.value)}
            placeholder={field.placeholder}
            rows={3}
          />
        )

      case 'number':
        return (
          <Input
            type="number"
            value={(value as number) ?? field.defaultValue ?? ''}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => onChange(parseFloat(e.target.value) || 0)}
            placeholder={field.placeholder}
            min={field.validation?.min}
            max={field.validation?.max}
          />
        )

      case 'select':
        return (
          <Select
            value={(value as string) ?? (field.defaultValue as string) ?? ''}
            onValueChange={onChange}
          >
            <SelectTrigger>
              <SelectValue placeholder={field.placeholder || 'Select an option'} />
            </SelectTrigger>
            <SelectContent>
              {field.options?.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )

      case 'boolean':
        return (
          <div className="flex items-center space-x-2">
            <Switch
              checked={(value as boolean) ?? (field.defaultValue as boolean) ?? false}
              onCheckedChange={onChange}
            />
            <span className="text-sm text-muted-foreground">
              {value ? 'Enabled' : 'Disabled'}
            </span>
          </div>
        )

      case 'json':
        return (
          <Textarea
            value={
              typeof value === 'object'
                ? JSON.stringify(value, null, 2)
                : (value as string) ?? ''
            }
            onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => {
              try {
                onChange(JSON.parse(e.target.value))
              } catch {
                // Keep as string if not valid JSON
                onChange(e.target.value)
              }
            }}
            placeholder={field.placeholder}
            rows={4}
            className="font-mono text-sm"
          />
        )

      case 'cron':
        return (
          <div className="space-y-2">
            <Input
              value={(value as string) ?? field.defaultValue ?? ''}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
              placeholder={field.placeholder || '* * * * *'}
              className="font-mono"
            />
            <div className="text-xs text-muted-foreground">
              Format: minute hour day month weekday
            </div>
          </div>
        )

      case 'duration':
        return (
          <Input
            value={(value as string) ?? field.defaultValue ?? ''}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
            placeholder={field.placeholder || 'e.g., 5m, 1h, 30s'}
          />
        )

      default:
        return (
          <Input
            value={(value as string) ?? ''}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
            placeholder={field.placeholder}
          />
        )
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-1">
        <Label htmlFor={field.key}>
          {field.label}
          {field.required && <span className="text-destructive ml-0.5">*</span>}
        </Label>
        {field.description && (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger>
                <Info className="w-3.5 h-3.5 text-muted-foreground" />
              </TooltipTrigger>
              <TooltipContent side="top" className="max-w-[200px]">
                {field.description}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        )}
      </div>
      {renderInput()}
    </div>
  )
}
