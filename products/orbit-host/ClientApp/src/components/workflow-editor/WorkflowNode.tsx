import { memo, useMemo } from 'react'
import { Handle, Position, type NodeProps } from '@xyflow/react'
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
  AlertCircle,
  CheckCircle2,
  Settings,
} from 'lucide-react'
import type { WorkflowNodeData, NodeCategory } from './types'
import { useWorkflowEditorStore } from './store'
import { cn } from '@/lib/utils'

// Icon mapping for node types
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
const CATEGORY_COLORS: Record<NodeCategory, { bg: string; border: string; accent: string }> = {
  trigger: {
    bg: 'bg-green-50 dark:bg-green-950/30',
    border: 'border-green-500',
    accent: 'bg-green-500',
  },
  action: {
    bg: 'bg-blue-50 dark:bg-blue-950/30',
    border: 'border-blue-500',
    accent: 'bg-blue-500',
  },
  logic: {
    bg: 'bg-amber-50 dark:bg-amber-950/30',
    border: 'border-amber-500',
    accent: 'bg-amber-500',
  },
  transform: {
    bg: 'bg-purple-50 dark:bg-purple-950/30',
    border: 'border-purple-500',
    accent: 'bg-purple-500',
  },
  integration: {
    bg: 'bg-pink-50 dark:bg-pink-950/30',
    border: 'border-pink-500',
    accent: 'bg-pink-500',
  },
}

interface WorkflowNodeProps extends NodeProps {
  data: WorkflowNodeData
}

function WorkflowNodeComponent({ id, data, selected }: WorkflowNodeProps) {
  const selectNode = useWorkflowEditorStore((state) => state.selectNode)
  const selectedNodeId = useWorkflowEditorStore((state) => state.editor.selectedNodeId)

  const isSelected = selected || selectedNodeId === id
  const colors = CATEGORY_COLORS[data.category]
  const Icon = ICON_MAP[data.icon] || Zap

  // Determine handle positions based on node category
  const handles = useMemo(() => {
    const result = {
      hasInput: data.category !== 'trigger',
      hasOutput: true,
      outputCount: data.nodeType === 'logic-condition' ? 2 : data.nodeType === 'logic-switch' ? 3 : 1,
    }
    return result
  }, [data.category, data.nodeType])

  return (
    <div
      className={cn(
        'relative min-w-[180px] max-w-[220px] rounded-lg border-2 shadow-md transition-all duration-200',
        colors.bg,
        colors.border,
        isSelected && 'ring-2 ring-offset-2 ring-blue-500 dark:ring-offset-gray-900',
        data.hasError && 'ring-2 ring-red-500'
      )}
      onClick={() => selectNode(id)}
    >
      {/* Input Handle */}
      {handles.hasInput && (
        <Handle
          type="target"
          position={Position.Top}
          className={cn(
            'w-3 h-3 !bg-gray-400 border-2 border-white dark:border-gray-800',
            'hover:!bg-gray-600 transition-colors'
          )}
        />
      )}

      {/* Node Header */}
      <div className={cn('flex items-center gap-2 px-3 py-2 rounded-t-md', colors.accent)}>
        <Icon className="w-4 h-4 text-white" />
        <span className="text-sm font-medium text-white truncate">{data.label}</span>
      </div>

      {/* Node Body */}
      <div className="px-3 py-2">
        {data.description && (
          <p className="text-xs text-muted-foreground line-clamp-2 mb-2">{data.description}</p>
        )}

        {/* Configuration Status */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-1">
            {data.isConfigured ? (
              <CheckCircle2 className="w-3.5 h-3.5 text-green-500" />
            ) : (
              <Settings className="w-3.5 h-3.5 text-muted-foreground" />
            )}
            <span className="text-xs text-muted-foreground">
              {data.isConfigured ? 'Configured' : 'Click to configure'}
            </span>
          </div>
        </div>

        {/* Error Message */}
        {data.hasError && data.errorMessage && (
          <div className="mt-2 flex items-start gap-1 p-1.5 bg-red-100 dark:bg-red-900/30 rounded text-xs text-red-600 dark:text-red-400">
            <AlertCircle className="w-3.5 h-3.5 flex-shrink-0 mt-0.5" />
            <span className="line-clamp-2">{data.errorMessage}</span>
          </div>
        )}
      </div>

      {/* Output Handles */}
      {handles.hasOutput && (
        <>
          {handles.outputCount === 1 && (
            <Handle
              type="source"
              position={Position.Bottom}
              className={cn(
                'w-3 h-3 !bg-gray-400 border-2 border-white dark:border-gray-800',
                'hover:!bg-gray-600 transition-colors'
              )}
            />
          )}

          {/* Conditional outputs for If/Else */}
          {data.nodeType === 'logic-condition' && (
            <>
              <Handle
                type="source"
                position={Position.Bottom}
                id="true"
                className={cn(
                  'w-3 h-3 !bg-green-500 border-2 border-white dark:border-gray-800',
                  'hover:!bg-green-600 transition-colors',
                  '!left-[30%]'
                )}
              />
              <Handle
                type="source"
                position={Position.Bottom}
                id="false"
                className={cn(
                  'w-3 h-3 !bg-red-500 border-2 border-white dark:border-gray-800',
                  'hover:!bg-red-600 transition-colors',
                  '!left-[70%]'
                )}
              />
              <div className="absolute bottom-0 left-0 right-0 flex justify-between px-6 -mb-5 text-[10px] text-muted-foreground">
                <span>True</span>
                <span>False</span>
              </div>
            </>
          )}

          {/* Multiple outputs for Switch */}
          {data.nodeType === 'logic-switch' && (
            <>
              <Handle
                type="source"
                position={Position.Bottom}
                id="case-0"
                className={cn(
                  'w-3 h-3 !bg-blue-500 border-2 border-white dark:border-gray-800',
                  'hover:!bg-blue-600 transition-colors',
                  '!left-[25%]'
                )}
              />
              <Handle
                type="source"
                position={Position.Bottom}
                id="case-1"
                className={cn(
                  'w-3 h-3 !bg-purple-500 border-2 border-white dark:border-gray-800',
                  'hover:!bg-purple-600 transition-colors',
                  '!left-[50%]'
                )}
              />
              <Handle
                type="source"
                position={Position.Bottom}
                id="default"
                className={cn(
                  'w-3 h-3 !bg-gray-500 border-2 border-white dark:border-gray-800',
                  'hover:!bg-gray-600 transition-colors',
                  '!left-[75%]'
                )}
              />
            </>
          )}
        </>
      )}
    </div>
  )
}

export const WorkflowNode = memo(WorkflowNodeComponent)
