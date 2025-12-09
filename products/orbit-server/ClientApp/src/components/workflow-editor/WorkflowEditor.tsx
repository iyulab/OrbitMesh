import { useCallback, useEffect, useState } from 'react'
import { ReactFlowProvider } from '@xyflow/react'
import { useWorkflowEditorStore } from './store'
import { WorkflowCanvas } from './WorkflowCanvas'
import { NodePalette } from './NodePalette'
import { NodeConfigPanel } from './NodeConfigPanel'
import { cn } from '@/lib/utils'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import {
  AlertTriangle,
  CheckCircle2,
  PanelLeftClose,
  PanelLeftOpen,
  PanelRightClose,
  PanelRightOpen,
  FileJson,
  Upload,
  AlertCircle,
} from 'lucide-react'
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

interface WorkflowEditorProps {
  className?: string
  onSave?: (workflow: { nodes: unknown[]; edges: unknown[]; name: string; description: string }) => void
}

export function WorkflowEditor({ className, onSave }: WorkflowEditorProps) {
  const [leftPanelOpen, setLeftPanelOpen] = useState(true)
  const [rightPanelOpen, setRightPanelOpen] = useState(true)
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const [importJson, setImportJson] = useState('')
  const [importError, setImportError] = useState<string | null>(null)

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

  // Handle save
  const handleSave = useCallback(() => {
    const errors = validateWorkflow()
    const hasErrors = errors.some((e) => e.severity === 'error')

    if (hasErrors) {
      // Don't save if there are errors
      return
    }

    const workflow = exportWorkflow()
    onSave?.(workflow)
  }, [validateWorkflow, exportWorkflow, onSave])

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

  // Count errors and warnings
  const errorCount = editor.validationErrors.filter((e) => e.severity === 'error').length
  const warningCount = editor.validationErrors.filter((e) => e.severity === 'warning').length

  return (
    <ReactFlowProvider>
      <div className={cn('flex flex-col h-full', className)}>
        {/* Top Bar */}
        <div className="flex items-center justify-between px-4 py-2 border-b bg-background">
          <div className="flex items-center gap-4">
            {/* Toggle Left Panel */}
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
          </div>

          <div className="flex items-center gap-2">
            {/* Validation Status */}
            <div className="flex items-center gap-2 mr-4">
              {errorCount > 0 ? (
                <div className="flex items-center gap-1 text-destructive">
                  <AlertCircle className="w-4 h-4" />
                  <span className="text-sm">{errorCount} error{errorCount > 1 ? 's' : ''}</span>
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

            <Button variant="outline" size="sm" onClick={handleExportJson}>
              <FileJson className="w-4 h-4 mr-1" />
              Export
            </Button>

            {/* Toggle Right Panel */}
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
          </div>
        </div>

        {/* Main Content */}
        <div className="flex-1 flex overflow-hidden">
          {/* Left Panel - Node Palette */}
          {leftPanelOpen && (
            <NodePalette className="w-64 flex-shrink-0" />
          )}

          {/* Center - Canvas */}
          <div className="flex-1 relative">
            <WorkflowCanvas onSave={handleSave} />

            {/* Validation Errors Overlay */}
            {editor.validationErrors.length > 0 && (
              <div className="absolute bottom-4 left-4 max-w-sm">
                <Alert variant={errorCount > 0 ? 'destructive' : 'default'}>
                  {errorCount > 0 ? (
                    <AlertCircle className="w-4 h-4" />
                  ) : (
                    <AlertTriangle className="w-4 h-4" />
                  )}
                  <AlertTitle>
                    {errorCount > 0 ? 'Validation Errors' : 'Warnings'}
                  </AlertTitle>
                  <AlertDescription>
                    <ul className="mt-2 space-y-1 text-sm">
                      {editor.validationErrors.slice(0, 3).map((error, index) => (
                        <li key={index} className="flex items-start gap-1">
                          <span className={cn(
                            'w-1.5 h-1.5 rounded-full mt-1.5 flex-shrink-0',
                            error.severity === 'error' ? 'bg-destructive' : 'bg-amber-500'
                          )} />
                          {error.message}
                        </li>
                      ))}
                      {editor.validationErrors.length > 3 && (
                        <li className="text-muted-foreground">
                          +{editor.validationErrors.length - 3} more...
                        </li>
                      )}
                    </ul>
                  </AlertDescription>
                </Alert>
              </div>
            )}
          </div>

          {/* Right Panel - Node Config */}
          {rightPanelOpen && (
            <NodeConfigPanel className="w-80 flex-shrink-0" />
          )}
        </div>
      </div>
    </ReactFlowProvider>
  )
}
