import type { Node, Edge } from '@xyflow/react'

// Node categories for the palette
export type NodeCategory = 'trigger' | 'action' | 'logic' | 'transform' | 'integration'

// Node types available in the editor
export type WorkflowNodeType =
  | 'trigger-manual'
  | 'trigger-schedule'
  | 'trigger-webhook'
  | 'action-job'
  | 'action-http'
  | 'action-delay'
  | 'logic-condition'
  | 'logic-switch'
  | 'logic-loop'
  | 'transform-filter'
  | 'transform-map'
  | 'transform-aggregate'

// Base data for all workflow nodes
export interface WorkflowNodeData extends Record<string, unknown> {
  label: string
  description?: string
  nodeType: WorkflowNodeType
  category: NodeCategory
  icon: string
  config: Record<string, unknown>
  isConfigured: boolean
  hasError?: boolean
  errorMessage?: string
}

// Workflow node with typed data
export type WorkflowNode = Node<WorkflowNodeData>

// Workflow edge with typed data
export interface WorkflowEdgeData extends Record<string, unknown> {
  label?: string
  condition?: string
}

export type WorkflowEdge = Edge<WorkflowEdgeData>

// Node definition for the palette
export interface NodeDefinition {
  type: WorkflowNodeType
  category: NodeCategory
  label: string
  description: string
  icon: string
  defaultConfig: Record<string, unknown>
  configSchema: ConfigField[]
  color: string
}

// Configuration field schema
export interface ConfigField {
  key: string
  label: string
  type: 'text' | 'textarea' | 'number' | 'select' | 'boolean' | 'json' | 'cron' | 'duration'
  placeholder?: string
  defaultValue?: unknown
  required?: boolean
  options?: { value: string; label: string }[]
  description?: string
  validation?: {
    min?: number
    max?: number
    pattern?: string
    message?: string
  }
}

// Workflow template
export interface WorkflowTemplate {
  id: string
  name: string
  description: string
  category: string
  icon: string
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
  tags: string[]
}

// Workflow definition for saving
export interface WorkflowDefinition {
  id?: string
  name: string
  description?: string
  version: string
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
  variables?: Record<string, unknown>
  isActive?: boolean
}

// Editor state
export interface EditorState {
  isDirty: boolean
  selectedNodeId: string | null
  selectedEdgeId: string | null
  isValidating: boolean
  validationErrors: ValidationError[]
  zoom: number
  isPanelOpen: boolean
}

// Validation error
export interface ValidationError {
  nodeId?: string
  edgeId?: string
  field?: string
  message: string
  severity: 'error' | 'warning'
}

// Undo/Redo history entry
export interface HistoryEntry {
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
  timestamp: number
}
