import { useCallback, useEffect, useState } from 'react'
import { ReactFlowProvider } from '@xyflow/react'
import { stringify as yamlStringify, parse as yamlParse } from 'yaml'
import { useWorkflowEditorStore } from './store'
import { WorkflowCanvas } from './WorkflowCanvas'
import { WorkflowYamlEditor } from './WorkflowYamlEditor'
import { NodePalette } from './NodePalette'
import { NodeConfigPanel } from './NodeConfigPanel'
import { WorkflowTemplatesDialog } from './WorkflowTemplates'
import { cn } from '@/lib/utils'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  AlertTriangle,
  CheckCircle2,
  PanelLeftClose,
  PanelLeftOpen,
  PanelRightClose,
  PanelRightOpen,
  FileJson,
  FileCode,
  Upload,
  AlertCircle,
  LayoutTemplate,
  Download,
  ChevronDown,
  ChevronUp,
} from 'lucide-react'
import type { WorkflowTemplate, WorkflowNode, WorkflowEdge } from './types'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
} from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'

type EditorTab = 'designer' | 'editor'

interface WorkflowEditorProps {
  className?: string
  onSave?: (workflow: { nodes: unknown[]; edges: unknown[]; name: string; description: string }) => void
}

export function WorkflowEditor({ className, onSave }: WorkflowEditorProps) {
  const [activeTab, setActiveTab] = useState<EditorTab>('designer')
  const [leftPanelOpen, setLeftPanelOpen] = useState(true)
  const [rightPanelOpen, setRightPanelOpen] = useState(true)
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const [importJson, setImportJson] = useState('')
  const [importError, setImportError] = useState<string | null>(null)
  const [templatesDialogOpen, setTemplatesDialogOpen] = useState(false)
  const [yamlValue, setYamlValue] = useState('')
  const [yamlErrors, setYamlErrors] = useState<string[]>([])
  const [validationPanelExpanded, setValidationPanelExpanded] = useState(true)

  // Store state
  const workflowName = useWorkflowEditorStore((state) => state.workflowName)
  const setWorkflowName = useWorkflowEditorStore((state) => state.setWorkflowName)
  const editor = useWorkflowEditorStore((state) => state.editor)
  const exportWorkflow = useWorkflowEditorStore((state) => state.exportWorkflow)
  const loadWorkflow = useWorkflowEditorStore((state) => state.loadWorkflow)
  const validateWorkflow = useWorkflowEditorStore((state) => state.validateWorkflow)

  // Validate on mount and when nodes change
  useEffect(() => {
    validateWorkflow()
  }, [validateWorkflow])

  // Sync designer to YAML when switching to editor tab
  const syncToYaml = useCallback(() => {
    const workflow = exportWorkflow()
    const yamlData = {
      name: workflow.name,
      description: workflow.description,
      version: '1.0.0',
      steps: workflow.nodes.map((node) => ({
        id: node.id,
        type: node.data.nodeType,
        name: node.data.label,
        description: node.data.description,
        config: node.data.config,
        position: node.position,
      })),
      connections: workflow.edges.map((edge) => ({
        id: edge.id,
        from: edge.source,
        to: edge.target,
        fromHandle: edge.sourceHandle,
        toHandle: edge.targetHandle,
        label: edge.data?.label,
      })),
    }
    setYamlValue(yamlStringify(yamlData, { indent: 2 }))
  }, [exportWorkflow])

  // Sync YAML to designer when applying changes
  const syncFromYaml = useCallback(() => {
    try {
      const data = yamlParse(yamlValue) as {
        name?: string
        description?: string
        steps?: Array<{
          id: string
          type: string
          name: string
          description?: string
          config?: Record<string, unknown>
          position?: { x: number; y: number }
        }>
        connections?: Array<{
          id: string
          from: string
          to: string
          fromHandle?: string | null
          toHandle?: string | null
          label?: string
        }>
      }

      if (!data) {
        setYamlErrors(['Invalid YAML: Empty document'])
        return
      }

      // Convert YAML to nodes and edges
      const newNodes: WorkflowNode[] = (data.steps || []).map((step, index) => ({
        id: step.id || `node_${Date.now()}_${index}`,
        type: 'workflowNode',
        position: step.position || { x: 100 + index * 200, y: 100 },
        data: {
          label: step.name || 'Unnamed Step',
          description: step.description,
          nodeType: step.type as WorkflowNode['data']['nodeType'],
          category: getCategory(step.type) as WorkflowNode['data']['category'],
          icon: getIconForType(step.type),
          config: step.config || {},
          isConfigured: true,
        },
      }))

      const newEdges: WorkflowEdge[] = (data.connections || []).map((conn) => ({
        id: conn.id || `edge_${conn.from}_${conn.to}`,
        source: conn.from,
        target: conn.to,
        sourceHandle: conn.fromHandle,
        targetHandle: conn.toHandle,
        type: 'smoothstep',
        animated: true,
        data: conn.label ? { label: conn.label } : undefined,
      }))

      loadWorkflow(newNodes, newEdges, data.name || 'Untitled Workflow', data.description || '')
      setYamlErrors([])
    } catch (e) {
      setYamlErrors([e instanceof Error ? e.message : 'Invalid YAML format'])
    }
  }, [yamlValue, loadWorkflow])

  // Handle tab change
  const handleTabChange = useCallback((value: string) => {
    const newTab = value as EditorTab
    if (newTab === 'editor' && activeTab === 'designer') {
      syncToYaml()
    }
    setActiveTab(newTab)
  }, [activeTab, syncToYaml])

  // Handle save
  const handleSave = useCallback(() => {
    // If in editor mode, sync YAML to designer first
    if (activeTab === 'editor') {
      syncFromYaml()
    }

    const errors = validateWorkflow()
    const hasErrors = errors.some((e) => e.severity === 'error')

    if (hasErrors) {
      return
    }

    const workflow = exportWorkflow()
    onSave?.(workflow)
  }, [activeTab, syncFromYaml, validateWorkflow, exportWorkflow, onSave])

  // Handle export to JSON
  const handleExportJson = useCallback(() => {
    const workflow = exportWorkflow()
    const json = JSON.stringify(workflow, null, 2)
    const blob = new Blob([json], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${workflow.name.toLowerCase().replace(/\s+/g, '-')}.json`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }, [exportWorkflow])

  // Handle export to YAML
  const handleExportYaml = useCallback(() => {
    const workflow = exportWorkflow()
    const yamlData = {
      name: workflow.name,
      description: workflow.description,
      version: '1.0.0',
      steps: workflow.nodes.map((node) => ({
        id: node.id,
        type: node.data.nodeType,
        name: node.data.label,
        description: node.data.description,
        config: node.data.config,
        position: node.position,
      })),
      connections: workflow.edges.map((edge) => ({
        id: edge.id,
        from: edge.source,
        to: edge.target,
        fromHandle: edge.sourceHandle,
        toHandle: edge.targetHandle,
        label: edge.data?.label,
      })),
    }

    const yaml = yamlStringify(yamlData, { indent: 2 })
    const blob = new Blob([yaml], { type: 'text/yaml' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${workflow.name.toLowerCase().replace(/\s+/g, '-')}.yaml`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }, [exportWorkflow])

  // Handle import from JSON
  const handleImport = useCallback(() => {
    try {
      setImportError(null)
      const data = JSON.parse(importJson)

      if (!Array.isArray(data.nodes) || !Array.isArray(data.edges)) {
        throw new Error('Invalid workflow format: missing nodes or edges')
      }

      loadWorkflow(data.nodes, data.edges, data.name, data.description)
      setImportDialogOpen(false)
      setImportJson('')
    } catch (error) {
      setImportError(error instanceof Error ? error.message : 'Invalid JSON format')
    }
  }, [importJson, loadWorkflow])

  // Handle template selection
  const handleSelectTemplate = useCallback((template: WorkflowTemplate) => {
    loadWorkflow(template.nodes, template.edges, template.name, template.description)
  }, [loadWorkflow])

  // Count errors and warnings
  const errorCount = editor.validationErrors.filter((e) => e.severity === 'error').length
  const warningCount = editor.validationErrors.filter((e) => e.severity === 'warning').length
  const totalIssues = errorCount + warningCount + yamlErrors.length

  return (
    <ReactFlowProvider>
      <div className={cn('flex flex-col h-full', className)}>
        {/* Top Bar */}
        <div className="flex items-center justify-between px-4 py-2 border-b bg-background">
          <div className="flex items-center gap-4">
            {/* Toggle Left Panel (only in designer mode) */}
            {activeTab === 'designer' && (
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setLeftPanelOpen(!leftPanelOpen)}
                title={leftPanelOpen ? 'Hide node palette' : 'Show node palette'}
              >
                {leftPanelOpen ? (
                  <PanelLeftClose className="w-4 h-4" />
                ) : (
                  <PanelLeftOpen className="w-4 h-4" />
                )}
              </Button>
            )}

            {/* Workflow Name */}
            <div className="flex items-center gap-2">
              <Input
                value={workflowName}
                onChange={(e) => setWorkflowName(e.target.value)}
                className="w-64 h-8 font-medium"
                placeholder="Workflow name"
              />
              {editor.isDirty && (
                <span className="text-xs text-muted-foreground">(unsaved)</span>
              )}
            </div>

            {/* Designer / Editor Tabs */}
            <Tabs value={activeTab} onValueChange={handleTabChange}>
              <TabsList>
                <TabsTrigger value="designer">Designer</TabsTrigger>
                <TabsTrigger value="editor">Editor</TabsTrigger>
              </TabsList>
            </Tabs>
          </div>

          <div className="flex items-center gap-2">
            {/* Validation Status */}
            <div className="flex items-center gap-2 mr-4">
              {errorCount > 0 || yamlErrors.length > 0 ? (
                <div className="flex items-center gap-1 text-destructive">
                  <AlertCircle className="w-4 h-4" />
                  <span className="text-sm">{errorCount + yamlErrors.length} error{(errorCount + yamlErrors.length) > 1 ? 's' : ''}</span>
                </div>
              ) : warningCount > 0 ? (
                <div className="flex items-center gap-1 text-amber-500">
                  <AlertTriangle className="w-4 h-4" />
                  <span className="text-sm">{warningCount} warning{warningCount > 1 ? 's' : ''}</span>
                </div>
              ) : (
                <div className="flex items-center gap-1 text-green-500">
                  <CheckCircle2 className="w-4 h-4" />
                  <span className="text-sm">Valid</span>
                </div>
              )}
            </div>

            {/* Apply Changes (only in editor mode) */}
            {activeTab === 'editor' && (
              <Button variant="outline" size="sm" onClick={syncFromYaml}>
                Apply Changes
              </Button>
            )}

            {/* Templates */}
            <Button
              variant="outline"
              size="sm"
              onClick={() => setTemplatesDialogOpen(true)}
            >
              <LayoutTemplate className="w-4 h-4 mr-1" />
              Templates
            </Button>

            {/* Import/Export */}
            <Dialog open={importDialogOpen} onOpenChange={setImportDialogOpen}>
              <DialogTrigger asChild>
                <Button variant="outline" size="sm">
                  <Upload className="w-4 h-4 mr-1" />
                  Import
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Import Workflow</DialogTitle>
                  <DialogDescription>
                    Paste the workflow JSON to import an existing workflow
                  </DialogDescription>
                </DialogHeader>
                <div className="py-4">
                  <Textarea
                    value={importJson}
                    onChange={(e) => setImportJson(e.target.value)}
                    placeholder='{"nodes": [], "edges": [], "name": "My Workflow"}'
                    rows={10}
                    className="font-mono text-sm"
                  />
                  {importError && (
                    <Alert variant="destructive" className="mt-4">
                      <AlertCircle className="w-4 h-4" />
                      <AlertTitle>Import Error</AlertTitle>
                      <AlertDescription>{importError}</AlertDescription>
                    </Alert>
                  )}
                </div>
                <DialogFooter>
                  <Button variant="outline" onClick={() => setImportDialogOpen(false)}>
                    Cancel
                  </Button>
                  <Button onClick={handleImport}>Import</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="sm">
                  <Download className="w-4 h-4 mr-1" />
                  Export
                  <ChevronDown className="w-3 h-3 ml-1" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={handleExportJson}>
                  <FileJson className="w-4 h-4 mr-2" />
                  Export as JSON
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleExportYaml}>
                  <FileCode className="w-4 h-4 mr-2" />
                  Export as YAML
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            {/* Toggle Right Panel (only in designer mode) */}
            {activeTab === 'designer' && (
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setRightPanelOpen(!rightPanelOpen)}
                title={rightPanelOpen ? 'Hide config panel' : 'Show config panel'}
              >
                {rightPanelOpen ? (
                  <PanelRightClose className="w-4 h-4" />
                ) : (
                  <PanelRightOpen className="w-4 h-4" />
                )}
              </Button>
            )}
          </div>
        </div>

        {/* Main Content */}
        <div className="flex-1 flex flex-col overflow-hidden">
          <div className="flex-1 flex overflow-hidden">
            {/* Left Panel - Node Palette (only in designer mode) */}
            {activeTab === 'designer' && leftPanelOpen && (
              <NodePalette className="w-64 flex-shrink-0" />
            )}

            {/* Center - Canvas or YAML Editor */}
            <div className="flex-1 relative overflow-hidden">
              {activeTab === 'designer' ? (
                <WorkflowCanvas onSave={handleSave} />
              ) : (
                <WorkflowYamlEditor
                  value={yamlValue}
                  onChange={setYamlValue}
                  onValidationError={setYamlErrors}
                  className="h-full"
                />
              )}
            </div>

            {/* Right Panel - Node Config (only in designer mode) */}
            {activeTab === 'designer' && rightPanelOpen && (
              <NodeConfigPanel className="w-80 flex-shrink-0" />
            )}
          </div>

          {/* Validation Errors Panel (bottom) */}
          {totalIssues > 0 && (
            <div className="border-t bg-background">
              {/* Panel Header */}
              <div
                className="flex items-center justify-between px-4 py-2 cursor-pointer hover:bg-muted/50"
                onClick={() => setValidationPanelExpanded(!validationPanelExpanded)}
              >
                <div className="flex items-center gap-3">
                  {(errorCount > 0 || yamlErrors.length > 0) && (
                    <div className="flex items-center gap-1 text-destructive">
                      <AlertCircle className="w-4 h-4" />
                      <span className="text-sm font-medium">
                        {errorCount + yamlErrors.length} error{(errorCount + yamlErrors.length) !== 1 ? 's' : ''}
                      </span>
                    </div>
                  )}
                  {warningCount > 0 && (
                    <div className="flex items-center gap-1 text-amber-500">
                      <AlertTriangle className="w-4 h-4" />
                      <span className="text-sm font-medium">
                        {warningCount} warning{warningCount !== 1 ? 's' : ''}
                      </span>
                    </div>
                  )}
                </div>
                {validationPanelExpanded ? (
                  <ChevronDown className="w-4 h-4 text-muted-foreground" />
                ) : (
                  <ChevronUp className="w-4 h-4 text-muted-foreground" />
                )}
              </div>

              {/* Panel Content */}
              {validationPanelExpanded && (
                <div className="max-h-40 overflow-y-auto border-t">
                  {/* YAML Errors */}
                  {yamlErrors.map((error, index) => (
                    <div key={`yaml-${index}`} className="flex items-start gap-2 px-4 py-2 text-sm">
                      <AlertCircle className="w-4 h-4 text-destructive mt-0.5 flex-shrink-0" />
                      <span className="text-destructive">{error}</span>
                    </div>
                  ))}
                  {/* Validation Errors */}
                  {editor.validationErrors.map((error, index) => (
                    <div
                      key={index}
                      className={cn(
                        'flex items-start gap-2 px-4 py-2 text-sm',
                        error.nodeId && 'cursor-pointer hover:bg-muted/50'
                      )}
                      onClick={() => {
                        if (error.nodeId) {
                          useWorkflowEditorStore.getState().selectNode(error.nodeId)
                          setActiveTab('designer')
                        }
                      }}
                    >
                      {error.severity === 'error' ? (
                        <AlertCircle className="w-4 h-4 text-destructive mt-0.5 flex-shrink-0" />
                      ) : (
                        <AlertTriangle className="w-4 h-4 text-amber-500 mt-0.5 flex-shrink-0" />
                      )}
                      <span className={error.severity === 'error' ? 'text-destructive' : 'text-amber-600'}>
                        {error.message}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Templates Dialog */}
      <WorkflowTemplatesDialog
        open={templatesDialogOpen}
        onOpenChange={setTemplatesDialogOpen}
        onSelectTemplate={handleSelectTemplate}
      />
    </ReactFlowProvider>
  )
}

// Helper function to determine category from node type
function getCategory(nodeType: string): string {
  if (nodeType.startsWith('trigger-')) return 'trigger'
  if (nodeType.startsWith('action-')) return 'action'
  if (nodeType.startsWith('logic-')) return 'logic'
  if (nodeType.startsWith('transform-')) return 'transform'
  return 'action'
}

// Helper function to get icon name for node type
function getIconForType(nodeType: string): string {
  const iconMap: Record<string, string> = {
    'trigger-manual': 'Play',
    'trigger-schedule': 'Clock',
    'trigger-webhook': 'Webhook',
    'action-job': 'Terminal',
    'action-http': 'Globe',
    'action-delay': 'Timer',
    'logic-condition': 'GitBranch',
    'logic-switch': 'Route',
    'logic-loop': 'Repeat',
    'transform-filter': 'Filter',
    'transform-map': 'ArrowRightLeft',
    'transform-aggregate': 'Combine',
  }
  return iconMap[nodeType] || 'Box'
}
