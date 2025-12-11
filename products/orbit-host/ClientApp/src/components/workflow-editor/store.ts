import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import {
  addEdge,
  applyNodeChanges,
  applyEdgeChanges,
  type Connection,
  type NodeChange,
  type EdgeChange,
  type Node,
  type Edge,
} from '@xyflow/react'
import type {
  WorkflowNode,
  WorkflowEdge,
  WorkflowNodeData,
  WorkflowNodeType,
  EditorState,
  HistoryEntry,
  ValidationError,
} from './types'
import { NODE_DEFINITIONS } from './node-definitions'

const MAX_HISTORY_SIZE = 50

interface WorkflowEditorState {
  // Workflow data
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
  workflowName: string
  workflowDescription: string

  // Editor state
  editor: EditorState

  // History for undo/redo
  history: HistoryEntry[]
  historyIndex: number

  // Actions
  setNodes: (nodes: WorkflowNode[]) => void
  setEdges: (edges: WorkflowEdge[]) => void
  onNodesChange: (changes: NodeChange<Node>[]) => void
  onEdgesChange: (changes: EdgeChange<Edge>[]) => void
  onConnect: (connection: Connection) => void

  // Node operations
  addNode: (type: WorkflowNodeType, position: { x: number; y: number }) => string
  updateNodeConfig: (nodeId: string, config: Record<string, unknown>) => void
  updateNodeData: (nodeId: string, data: Partial<WorkflowNodeData>) => void
  deleteNode: (nodeId: string) => void
  duplicateNode: (nodeId: string) => void

  // Edge operations
  deleteEdge: (edgeId: string) => void
  updateEdgeLabel: (edgeId: string, label: string) => void

  // Selection
  selectNode: (nodeId: string | null) => void
  selectEdge: (edgeId: string | null) => void
  clearSelection: () => void

  // Workflow metadata
  setWorkflowName: (name: string) => void
  setWorkflowDescription: (description: string) => void

  // Validation
  validateWorkflow: () => ValidationError[]

  // History operations
  undo: () => void
  redo: () => void
  pushHistory: () => void

  // Editor state
  setIsDirty: (isDirty: boolean) => void
  togglePanel: () => void
  setZoom: (zoom: number) => void

  // Import/Export
  loadWorkflow: (nodes: WorkflowNode[], edges: WorkflowEdge[], name?: string, description?: string) => void
  clearWorkflow: () => void
  exportWorkflow: () => { nodes: WorkflowNode[]; edges: WorkflowEdge[]; name: string; description: string }
}

function generateNodeId(): string {
  return `node_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`
}

function generateEdgeId(source: string, target: string): string {
  return `edge_${source}_${target}_${Math.random().toString(36).substring(2, 5)}`
}

export const useWorkflowEditorStore = create<WorkflowEditorState>()(
  immer((set, get) => ({
    // Initial state
    nodes: [],
    edges: [],
    workflowName: 'Untitled Workflow',
    workflowDescription: '',

    editor: {
      isDirty: false,
      selectedNodeId: null,
      selectedEdgeId: null,
      isValidating: false,
      validationErrors: [],
      zoom: 1,
      isPanelOpen: true,
    },

    history: [],
    historyIndex: -1,

    // Node/Edge setters
    setNodes: (nodes) => {
      set((state) => {
        state.nodes = nodes
        state.editor.isDirty = true
      })
    },

    setEdges: (edges) => {
      set((state) => {
        state.edges = edges
        state.editor.isDirty = true
      })
    },

    onNodesChange: (changes) => {
      set((state) => {
        state.nodes = applyNodeChanges(changes, state.nodes) as WorkflowNode[]
        state.editor.isDirty = true
      })
    },

    onEdgesChange: (changes) => {
      set((state) => {
        state.edges = applyEdgeChanges(changes, state.edges) as WorkflowEdge[]
        state.editor.isDirty = true
      })
    },

    onConnect: (connection) => {
      set((state) => {
        const newEdge: WorkflowEdge = {
          id: generateEdgeId(connection.source!, connection.target!),
          source: connection.source!,
          target: connection.target!,
          sourceHandle: connection.sourceHandle,
          targetHandle: connection.targetHandle,
          type: 'smoothstep',
          animated: true,
        }
        state.edges = addEdge(newEdge, state.edges) as WorkflowEdge[]
        state.editor.isDirty = true
      })
      get().pushHistory()
    },

    // Node operations
    addNode: (type, position) => {
      const definition = NODE_DEFINITIONS[type]
      if (!definition) {
        console.error(`Unknown node type: ${type}`)
        return ''
      }

      const nodeId = generateNodeId()
      const newNode: WorkflowNode = {
        id: nodeId,
        type: 'workflowNode',
        position,
        data: {
          label: definition.label,
          description: definition.description,
          nodeType: type,
          category: definition.category,
          icon: definition.icon,
          config: { ...definition.defaultConfig },
          isConfigured: false,
        },
      }

      set((state) => {
        state.nodes.push(newNode)
        state.editor.isDirty = true
        state.editor.selectedNodeId = nodeId
      })

      get().pushHistory()
      return nodeId
    },

    updateNodeConfig: (nodeId, config) => {
      set((state) => {
        const node = state.nodes.find((n: WorkflowNode) => n.id === nodeId)
        if (node) {
          node.data.config = { ...node.data.config, ...config }
          node.data.isConfigured = true
          state.editor.isDirty = true
        }
      })
      get().pushHistory()
    },

    updateNodeData: (nodeId, data) => {
      set((state) => {
        const node = state.nodes.find((n: WorkflowNode) => n.id === nodeId)
        if (node) {
          node.data = { ...node.data, ...data }
          state.editor.isDirty = true
        }
      })
    },

    deleteNode: (nodeId) => {
      set((state) => {
        state.nodes = state.nodes.filter((n: WorkflowNode) => n.id !== nodeId)
        state.edges = state.edges.filter(
          (e: WorkflowEdge) => e.source !== nodeId && e.target !== nodeId
        )
        if (state.editor.selectedNodeId === nodeId) {
          state.editor.selectedNodeId = null
        }
        state.editor.isDirty = true
      })
      get().pushHistory()
    },

    duplicateNode: (nodeId) => {
      const state = get()
      const node = state.nodes.find((n) => n.id === nodeId)
      if (!node) return

      const newNodeId = generateNodeId()
      const newNode: WorkflowNode = {
        ...node,
        id: newNodeId,
        position: {
          x: node.position.x + 50,
          y: node.position.y + 50,
        },
        data: { ...node.data },
      }

      set((state) => {
        state.nodes.push(newNode)
        state.editor.isDirty = true
        state.editor.selectedNodeId = newNodeId
      })
      get().pushHistory()
    },

    // Edge operations
    deleteEdge: (edgeId) => {
      set((state) => {
        state.edges = state.edges.filter((e: WorkflowEdge) => e.id !== edgeId)
        if (state.editor.selectedEdgeId === edgeId) {
          state.editor.selectedEdgeId = null
        }
        state.editor.isDirty = true
      })
      get().pushHistory()
    },

    updateEdgeLabel: (edgeId, label) => {
      set((state) => {
        const edge = state.edges.find((e: WorkflowEdge) => e.id === edgeId)
        if (edge) {
          edge.data = { ...edge.data, label }
          state.editor.isDirty = true
        }
      })
    },

    // Selection
    selectNode: (nodeId) => {
      set((state) => {
        state.editor.selectedNodeId = nodeId
        state.editor.selectedEdgeId = null
      })
    },

    selectEdge: (edgeId) => {
      set((state) => {
        state.editor.selectedEdgeId = edgeId
        state.editor.selectedNodeId = null
      })
    },

    clearSelection: () => {
      set((state) => {
        state.editor.selectedNodeId = null
        state.editor.selectedEdgeId = null
      })
    },

    // Workflow metadata
    setWorkflowName: (name) => {
      set((state) => {
        state.workflowName = name
        state.editor.isDirty = true
      })
    },

    setWorkflowDescription: (description) => {
      set((state) => {
        state.workflowDescription = description
        state.editor.isDirty = true
      })
    },

    // Validation
    validateWorkflow: () => {
      const state = get()
      const errors: ValidationError[] = []

      // Check for empty workflow
      if (state.nodes.length === 0) {
        errors.push({
          message: 'Workflow must have at least one node',
          severity: 'error',
        })
      }

      // Check workflow name
      if (!state.workflowName || state.workflowName.trim().length === 0) {
        errors.push({
          message: 'Workflow name is required',
          severity: 'error',
        })
      } else if (state.workflowName.length > 100) {
        errors.push({
          message: 'Workflow name must be 100 characters or less',
          severity: 'error',
        })
      }

      // Check for trigger node
      const triggerNodes = state.nodes.filter((n) => n.data.category === 'trigger')
      if (triggerNodes.length === 0 && state.nodes.length > 0) {
        errors.push({
          message: 'Workflow should have a trigger node',
          severity: 'warning',
        })
      }

      // Check for multiple trigger nodes (usually not desired)
      if (triggerNodes.length > 1) {
        errors.push({
          message: 'Workflow has multiple trigger nodes - this may cause unexpected behavior',
          severity: 'warning',
        })
      }

      // Check for action nodes
      const actionNodes = state.nodes.filter((n) => n.data.category === 'action')
      if (actionNodes.length === 0 && state.nodes.length > 0) {
        errors.push({
          message: 'Workflow should have at least one action node',
          severity: 'warning',
        })
      }

      // Check each node configuration
      state.nodes.forEach((node) => {
        const definition = NODE_DEFINITIONS[node.data.nodeType]
        if (definition) {
          definition.configSchema.forEach((field) => {
            const value = node.data.config[field.key]

            // Required field check
            if (field.required && (value === undefined || value === null || value === '')) {
              errors.push({
                nodeId: node.id,
                field: field.key,
                message: `${node.data.label}: ${field.label} is required`,
                severity: 'error',
              })
            }

            // Type-specific validation
            if (value !== undefined && value !== null && value !== '') {
              // Number validation
              if (field.type === 'number') {
                const numValue = Number(value)
                if (isNaN(numValue)) {
                  errors.push({
                    nodeId: node.id,
                    field: field.key,
                    message: `${node.data.label}: ${field.label} must be a valid number`,
                    severity: 'error',
                  })
                }
              }

              // URL validation for webhook URLs
              if (field.key === 'url' || field.key === 'webhookUrl' || field.key === 'endpoint') {
                try {
                  new URL(String(value))
                } catch {
                  // Allow template variables like {{variable}}
                  if (!String(value).includes('{{')) {
                    errors.push({
                      nodeId: node.id,
                      field: field.key,
                      message: `${node.data.label}: ${field.label} must be a valid URL`,
                      severity: 'error',
                    })
                  }
                }
              }

              // Cron expression basic validation
              if (field.key === 'schedule' || field.key === 'cron') {
                const cronParts = String(value).trim().split(/\s+/)
                if (cronParts.length < 5 || cronParts.length > 6) {
                  errors.push({
                    nodeId: node.id,
                    field: field.key,
                    message: `${node.data.label}: Invalid cron expression format`,
                    severity: 'error',
                  })
                }
              }
            }
          })
        }

        // Check node label
        if (!node.data.label || node.data.label.trim().length === 0) {
          errors.push({
            nodeId: node.id,
            message: 'Node must have a name',
            severity: 'error',
          })
        }
      })

      // Check for duplicate node labels
      const labelCounts = new Map<string, number>()
      state.nodes.forEach((node) => {
        const label = node.data.label?.toLowerCase() || ''
        labelCounts.set(label, (labelCounts.get(label) || 0) + 1)
      })
      labelCounts.forEach((count, label) => {
        if (count > 1 && label) {
          errors.push({
            message: `Duplicate node name: "${label}" appears ${count} times`,
            severity: 'warning',
          })
        }
      })

      // Check for disconnected nodes
      state.nodes.forEach((node) => {
        const hasConnection = state.edges.some(
          (e) => e.source === node.id || e.target === node.id
        )
        if (!hasConnection && state.nodes.length > 1) {
          errors.push({
            nodeId: node.id,
            message: `Node "${node.data.label}" is not connected`,
            severity: 'warning',
          })
        }
      })

      // Check for circular references (simple cycle detection)
      const detectCycle = (): boolean => {
        const visited = new Set<string>()
        const recursionStack = new Set<string>()

        const dfs = (nodeId: string): boolean => {
          visited.add(nodeId)
          recursionStack.add(nodeId)

          const outgoingEdges = state.edges.filter((e) => e.source === nodeId)
          for (const edge of outgoingEdges) {
            if (!visited.has(edge.target)) {
              if (dfs(edge.target)) return true
            } else if (recursionStack.has(edge.target)) {
              return true
            }
          }

          recursionStack.delete(nodeId)
          return false
        }

        for (const node of state.nodes) {
          if (!visited.has(node.id)) {
            if (dfs(node.id)) return true
          }
        }
        return false
      }

      if (state.nodes.length > 0 && detectCycle()) {
        errors.push({
          message: 'Workflow contains a circular reference',
          severity: 'error',
        })
      }

      // Check for dead-end action nodes (nodes with no outgoing connections that aren't terminal)
      state.nodes.forEach((node) => {
        if (node.data.category === 'action') {
          const hasOutgoing = state.edges.some((e) => e.source === node.id)
          const hasIncoming = state.edges.some((e) => e.target === node.id)

          // If node has incoming but no outgoing and it's not explicitly a terminal node
          if (hasIncoming && !hasOutgoing && state.nodes.length > 2) {
            // This is fine - could be a terminal action
          }
        }
      })

      // Check for orphaned edges (edges pointing to non-existent nodes)
      const nodeIds = new Set(state.nodes.map((n) => n.id))
      state.edges.forEach((edge) => {
        if (!nodeIds.has(edge.source)) {
          errors.push({
            message: `Edge references non-existent source node`,
            severity: 'error',
          })
        }
        if (!nodeIds.has(edge.target)) {
          errors.push({
            message: `Edge references non-existent target node`,
            severity: 'error',
          })
        }
      })

      set((state) => {
        state.editor.validationErrors = errors
      })

      return errors
    },

    // History
    undo: () => {
      const { history, historyIndex } = get()
      if (historyIndex > 0) {
        const prevState = history[historyIndex - 1]
        set((state) => {
          state.nodes = prevState.nodes
          state.edges = prevState.edges
          state.historyIndex = historyIndex - 1
        })
      }
    },

    redo: () => {
      const { history, historyIndex } = get()
      if (historyIndex < history.length - 1) {
        const nextState = history[historyIndex + 1]
        set((state) => {
          state.nodes = nextState.nodes
          state.edges = nextState.edges
          state.historyIndex = historyIndex + 1
        })
      }
    },

    pushHistory: () => {
      const { nodes, edges, history, historyIndex } = get()

      // Remove future history if we're not at the end
      const newHistory = history.slice(0, historyIndex + 1)

      // Add current state
      newHistory.push({
        nodes: JSON.parse(JSON.stringify(nodes)),
        edges: JSON.parse(JSON.stringify(edges)),
        timestamp: Date.now(),
      })

      // Limit history size
      if (newHistory.length > MAX_HISTORY_SIZE) {
        newHistory.shift()
      }

      set((state) => {
        state.history = newHistory
        state.historyIndex = newHistory.length - 1
      })
    },

    // Editor state
    setIsDirty: (isDirty) => {
      set((state) => {
        state.editor.isDirty = isDirty
      })
    },

    togglePanel: () => {
      set((state) => {
        state.editor.isPanelOpen = !state.editor.isPanelOpen
      })
    },

    setZoom: (zoom) => {
      set((state) => {
        state.editor.zoom = zoom
      })
    },

    // Import/Export
    loadWorkflow: (nodes, edges, name = 'Untitled Workflow', description = '') => {
      set((state) => {
        state.nodes = nodes
        state.edges = edges
        state.workflowName = name
        state.workflowDescription = description
        state.editor.isDirty = false
        state.editor.selectedNodeId = null
        state.editor.selectedEdgeId = null
        state.history = []
        state.historyIndex = -1
      })
      get().pushHistory()
    },

    clearWorkflow: () => {
      set((state) => {
        state.nodes = []
        state.edges = []
        state.workflowName = 'Untitled Workflow'
        state.workflowDescription = ''
        state.editor.isDirty = false
        state.editor.selectedNodeId = null
        state.editor.selectedEdgeId = null
        state.history = []
        state.historyIndex = -1
      })
    },

    exportWorkflow: () => {
      const { nodes, edges, workflowName, workflowDescription } = get()
      return {
        nodes: JSON.parse(JSON.stringify(nodes)),
        edges: JSON.parse(JSON.stringify(edges)),
        name: workflowName,
        description: workflowDescription,
      }
    },
  }))
)
