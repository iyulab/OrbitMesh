import { useCallback, useRef, useMemo } from 'react'
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  Panel,
  type OnConnect,
  type OnNodesChange,
  type OnEdgesChange,
  BackgroundVariant,
  ConnectionLineType,
  MarkerType,
} from '@xyflow/react'
import { WorkflowNode } from './WorkflowNode'
import { useWorkflowEditorStore } from './store'
import type { WorkflowNodeType } from './types'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Undo2, Redo2, Save, Trash2 } from 'lucide-react'

// Define custom node types
const nodeTypes = {
  workflowNode: WorkflowNode,
}

// Default edge options
const defaultEdgeOptions = {
  type: 'smoothstep',
  animated: true,
  style: { strokeWidth: 2 },
  markerEnd: {
    type: MarkerType.ArrowClosed,
    width: 15,
    height: 15,
  },
}

interface WorkflowCanvasProps {
  onSave?: () => void
  className?: string
}

export function WorkflowCanvas({ onSave, className }: WorkflowCanvasProps) {
  const reactFlowWrapper = useRef<HTMLDivElement>(null)

  // Store state and actions
  const nodes = useWorkflowEditorStore((state) => state.nodes)
  const edges = useWorkflowEditorStore((state) => state.edges)
  const onNodesChange = useWorkflowEditorStore((state) => state.onNodesChange)
  const onEdgesChange = useWorkflowEditorStore((state) => state.onEdgesChange)
  const onConnect = useWorkflowEditorStore((state) => state.onConnect)
  const addNode = useWorkflowEditorStore((state) => state.addNode)
  const deleteNode = useWorkflowEditorStore((state) => state.deleteNode)
  const deleteEdge = useWorkflowEditorStore((state) => state.deleteEdge)
  const undo = useWorkflowEditorStore((state) => state.undo)
  const redo = useWorkflowEditorStore((state) => state.redo)
  const clearWorkflow = useWorkflowEditorStore((state) => state.clearWorkflow)
  const editor = useWorkflowEditorStore((state) => state.editor)
  const history = useWorkflowEditorStore((state) => state.history)
  const historyIndex = useWorkflowEditorStore((state) => state.historyIndex)

  const canUndo = historyIndex > 0
  const canRedo = historyIndex < history.length - 1

  // Handle node changes
  const handleNodesChange: OnNodesChange = useCallback(
    (changes) => {
      onNodesChange(changes)
    },
    [onNodesChange]
  )

  // Handle edge changes
  const handleEdgesChange: OnEdgesChange = useCallback(
    (changes) => {
      onEdgesChange(changes)
    },
    [onEdgesChange]
  )

  // Handle connections
  const handleConnect: OnConnect = useCallback(
    (connection) => {
      onConnect(connection)
    },
    [onConnect]
  )

  // Handle drop from node palette
  const handleDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault()
    event.dataTransfer.dropEffect = 'move'
  }, [])

  const handleDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault()

      const nodeType = event.dataTransfer.getData('application/workflow-node') as WorkflowNodeType
      if (!nodeType) return

      const reactFlowBounds = reactFlowWrapper.current?.getBoundingClientRect()
      if (!reactFlowBounds) return

      const position = {
        x: event.clientX - reactFlowBounds.left - 90,
        y: event.clientY - reactFlowBounds.top - 30,
      }

      addNode(nodeType, position)
    },
    [addNode]
  )

  // Handle keyboard shortcuts
  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent) => {
      // Undo: Ctrl/Cmd + Z
      if ((event.ctrlKey || event.metaKey) && event.key === 'z' && !event.shiftKey) {
        event.preventDefault()
        if (canUndo) undo()
      }

      // Redo: Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y
      if (
        ((event.ctrlKey || event.metaKey) && event.shiftKey && event.key === 'z') ||
        ((event.ctrlKey || event.metaKey) && event.key === 'y')
      ) {
        event.preventDefault()
        if (canRedo) redo()
      }

      // Delete selected node/edge
      if (event.key === 'Delete' || event.key === 'Backspace') {
        if (editor.selectedNodeId) {
          deleteNode(editor.selectedNodeId)
        } else if (editor.selectedEdgeId) {
          deleteEdge(editor.selectedEdgeId)
        }
      }

      // Save: Ctrl/Cmd + S
      if ((event.ctrlKey || event.metaKey) && event.key === 's') {
        event.preventDefault()
        onSave?.()
      }
    },
    [canUndo, canRedo, undo, redo, editor.selectedNodeId, editor.selectedEdgeId, deleteNode, deleteEdge, onSave]
  )

  // MiniMap node color based on category
  const miniMapNodeColor = useCallback((node: { data?: { category?: string } }) => {
    const category = node.data?.category
    switch (category) {
      case 'trigger':
        return '#22c55e'
      case 'action':
        return '#3b82f6'
      case 'logic':
        return '#f59e0b'
      case 'transform':
        return '#8b5cf6'
      default:
        return '#6b7280'
    }
  }, [])

  // Edge styles based on selection
  const styledEdges = useMemo(() => {
    return edges.map((edge) => ({
      ...edge,
      style: {
        ...edge.style,
        stroke: edge.id === editor.selectedEdgeId ? '#3b82f6' : '#94a3b8',
        strokeWidth: edge.id === editor.selectedEdgeId ? 3 : 2,
      },
    }))
  }, [edges, editor.selectedEdgeId])

  return (
    <div
      ref={reactFlowWrapper}
      className={cn('w-full h-full', className)}
      onKeyDown={handleKeyDown}
      tabIndex={0}
    >
      <ReactFlow
        nodes={nodes}
        edges={styledEdges}
        onNodesChange={handleNodesChange}
        onEdgesChange={handleEdgesChange}
        onConnect={handleConnect}
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        nodeTypes={nodeTypes}
        defaultEdgeOptions={defaultEdgeOptions}
        connectionLineType={ConnectionLineType.SmoothStep}
        fitView
        snapToGrid
        snapGrid={[15, 15]}
        deleteKeyCode={['Backspace', 'Delete']}
        className="bg-background"
      >
        {/* Background Grid */}
        <Background
          variant={BackgroundVariant.Dots}
          gap={20}
          size={1}
          className="!bg-muted/30"
        />

        {/* Controls */}
        <Controls
          position="bottom-right"
          className="!bg-background !border-border !shadow-md"
        />

        {/* Mini Map */}
        <MiniMap
          position="bottom-left"
          nodeColor={miniMapNodeColor}
          maskColor="rgba(0, 0, 0, 0.1)"
          className="!bg-background !border-border !shadow-md"
          pannable
          zoomable
        />

        {/* Top Toolbar */}
        <Panel position="top-right" className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={undo}
            disabled={!canUndo}
            title="Undo (Ctrl+Z)"
          >
            <Undo2 className="w-4 h-4" />
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={redo}
            disabled={!canRedo}
            title="Redo (Ctrl+Shift+Z)"
          >
            <Redo2 className="w-4 h-4" />
          </Button>
          <div className="w-px bg-border" />
          <Button
            variant="outline"
            size="sm"
            onClick={clearWorkflow}
            title="Clear Workflow"
            className="text-destructive hover:text-destructive"
          >
            <Trash2 className="w-4 h-4" />
          </Button>
          <Button
            variant="default"
            size="sm"
            onClick={onSave}
            disabled={!editor.isDirty}
            title="Save (Ctrl+S)"
          >
            <Save className="w-4 h-4 mr-1" />
            Save
          </Button>
        </Panel>

        {/* Empty State */}
        {nodes.length === 0 && (
          <Panel position="top-center" className="mt-20">
            <div className="text-center p-8 bg-background/80 rounded-lg border border-dashed border-muted-foreground/30">
              <h3 className="text-lg font-medium mb-2">Start Building Your Workflow</h3>
              <p className="text-muted-foreground text-sm mb-4">
                Drag and drop nodes from the left panel to create your workflow
              </p>
              <div className="flex justify-center gap-4 text-xs text-muted-foreground">
                <span className="flex items-center gap-1">
                  <div className="w-3 h-3 rounded-full bg-green-500" />
                  Triggers start the workflow
                </span>
                <span className="flex items-center gap-1">
                  <div className="w-3 h-3 rounded-full bg-blue-500" />
                  Actions perform tasks
                </span>
              </div>
            </div>
          </Panel>
        )}
      </ReactFlow>
    </div>
  )
}
