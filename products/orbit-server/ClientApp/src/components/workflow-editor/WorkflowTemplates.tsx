import { useState, useMemo } from 'react'
import {
  FileCode2,
  Clock,
  Globe,
  RefreshCw,
  Database,
  Shield,
  Bell,
  Workflow,
  Search,
  CheckCircle,
  ArrowRight,
  Zap,
  GitBranch,
  Filter,
} from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { cn } from '@/lib/utils'
import type { WorkflowTemplate, WorkflowNode, WorkflowEdge } from './types'

// Icon mapping
const ICON_MAP: Record<string, React.ComponentType<{ className?: string }>> = {
  FileCode2,
  Clock,
  Globe,
  RefreshCw,
  Database,
  Shield,
  Bell,
  Workflow,
  Zap,
  GitBranch,
  Filter,
}

// Category metadata
const TEMPLATE_CATEGORIES: Record<string, { label: string; color: string; icon: React.ComponentType<{ className?: string }> }> = {
  automation: { label: 'Automation', color: 'bg-blue-500', icon: Zap },
  integration: { label: 'Integration', color: 'bg-purple-500', icon: Globe },
  monitoring: { label: 'Monitoring', color: 'bg-green-500', icon: Bell },
  'data-processing': { label: 'Data Processing', color: 'bg-orange-500', icon: Database },
  devops: { label: 'DevOps', color: 'bg-red-500', icon: RefreshCw },
}

// Pre-built workflow templates
export const WORKFLOW_TEMPLATES: WorkflowTemplate[] = [
  {
    id: 'simple-scheduled-job',
    name: 'Scheduled Job',
    description: 'Run a job on a recurring schedule. Perfect for backups, cleanups, or periodic tasks.',
    category: 'automation',
    icon: 'Clock',
    tags: ['schedule', 'automation', 'cron'],
    nodes: [
      {
        id: 'trigger-1',
        type: 'workflowNode',
        position: { x: 250, y: 50 },
        data: {
          label: 'Schedule Trigger',
          description: 'Run every hour',
          nodeType: 'trigger-schedule',
          category: 'trigger',
          icon: 'Clock',
          config: {
            cronExpression: '0 * * * *',
            timezone: 'UTC',
            enabled: true,
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-1',
        type: 'workflowNode',
        position: { x: 250, y: 180 },
        data: {
          label: 'Execute Job',
          description: 'Run scheduled task',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'scheduled-task',
            agentSelector: 'any',
            timeout: 300,
            retryCount: 1,
          },
          isConfigured: false,
        },
      },
    ] as WorkflowNode[],
    edges: [
      {
        id: 'edge-1',
        source: 'trigger-1',
        target: 'action-1',
        type: 'smoothstep',
      },
    ] as WorkflowEdge[],
  },
  {
    id: 'webhook-processor',
    name: 'Webhook Processor',
    description: 'Process incoming webhook requests with conditional routing based on payload.',
    category: 'integration',
    icon: 'Globe',
    tags: ['webhook', 'api', 'integration'],
    nodes: [
      {
        id: 'trigger-1',
        type: 'workflowNode',
        position: { x: 250, y: 50 },
        data: {
          label: 'Webhook Trigger',
          description: 'Receive HTTP requests',
          nodeType: 'trigger-webhook',
          category: 'trigger',
          icon: 'Webhook',
          config: {
            method: 'POST',
            path: '/webhook/incoming',
            authentication: 'bearer',
          },
          isConfigured: true,
        },
      },
      {
        id: 'logic-1',
        type: 'workflowNode',
        position: { x: 250, y: 180 },
        data: {
          label: 'Check Event Type',
          description: 'Route based on event',
          nodeType: 'logic-condition',
          category: 'logic',
          icon: 'GitBranch',
          config: {
            field: 'body.eventType',
            operator: 'equals',
            value: 'alert',
            combineWith: 'and',
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-1',
        type: 'workflowNode',
        position: { x: 100, y: 330 },
        data: {
          label: 'Process Alert',
          description: 'Handle alert events',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'process-alert',
            agentSelector: 'any',
            timeout: 60,
          },
          isConfigured: false,
        },
      },
      {
        id: 'action-2',
        type: 'workflowNode',
        position: { x: 400, y: 330 },
        data: {
          label: 'Process Other',
          description: 'Handle other events',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'process-event',
            agentSelector: 'any',
            timeout: 60,
          },
          isConfigured: false,
        },
      },
    ] as WorkflowNode[],
    edges: [
      {
        id: 'edge-1',
        source: 'trigger-1',
        target: 'logic-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-2',
        source: 'logic-1',
        sourceHandle: 'true',
        target: 'action-1',
        type: 'smoothstep',
        data: { label: 'Alert' },
      },
      {
        id: 'edge-3',
        source: 'logic-1',
        sourceHandle: 'false',
        target: 'action-2',
        type: 'smoothstep',
        data: { label: 'Other' },
      },
    ] as WorkflowEdge[],
  },
  {
    id: 'data-sync-pipeline',
    name: 'Data Sync Pipeline',
    description: 'Fetch data from an API, transform it, and sync to multiple destinations.',
    category: 'data-processing',
    icon: 'Database',
    tags: ['data', 'sync', 'etl', 'transform'],
    nodes: [
      {
        id: 'trigger-1',
        type: 'workflowNode',
        position: { x: 250, y: 50 },
        data: {
          label: 'Manual Trigger',
          description: 'Start data sync',
          nodeType: 'trigger-manual',
          category: 'trigger',
          icon: 'Play',
          config: {
            buttonLabel: 'Sync Now',
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-1',
        type: 'workflowNode',
        position: { x: 250, y: 180 },
        data: {
          label: 'Fetch Data',
          description: 'GET from source API',
          nodeType: 'action-http',
          category: 'action',
          icon: 'Globe',
          config: {
            method: 'GET',
            url: 'https://api.example.com/data',
            timeout: 30,
          },
          isConfigured: false,
        },
      },
      {
        id: 'transform-1',
        type: 'workflowNode',
        position: { x: 250, y: 310 },
        data: {
          label: 'Transform Data',
          description: 'Map fields to target format',
          nodeType: 'transform-map',
          category: 'transform',
          icon: 'Shuffle',
          config: {
            inputField: 'response.data',
            outputField: 'transformedData',
            mappings: [],
          },
          isConfigured: false,
        },
      },
      {
        id: 'action-2',
        type: 'workflowNode',
        position: { x: 250, y: 440 },
        data: {
          label: 'Send to Destination',
          description: 'POST to target API',
          nodeType: 'action-http',
          category: 'action',
          icon: 'Globe',
          config: {
            method: 'POST',
            url: 'https://api.destination.com/import',
            timeout: 60,
          },
          isConfigured: false,
        },
      },
    ] as WorkflowNode[],
    edges: [
      {
        id: 'edge-1',
        source: 'trigger-1',
        target: 'action-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-2',
        source: 'action-1',
        target: 'transform-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-3',
        source: 'transform-1',
        target: 'action-2',
        type: 'smoothstep',
      },
    ] as WorkflowEdge[],
  },
  {
    id: 'health-check-monitor',
    name: 'Health Check Monitor',
    description: 'Periodically check service health and send alerts on failures.',
    category: 'monitoring',
    icon: 'Bell',
    tags: ['health', 'monitoring', 'alert', 'uptime'],
    nodes: [
      {
        id: 'trigger-1',
        type: 'workflowNode',
        position: { x: 250, y: 50 },
        data: {
          label: 'Every 5 Minutes',
          description: 'Check health periodically',
          nodeType: 'trigger-schedule',
          category: 'trigger',
          icon: 'Clock',
          config: {
            cronExpression: '*/5 * * * *',
            timezone: 'UTC',
            enabled: true,
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-1',
        type: 'workflowNode',
        position: { x: 250, y: 180 },
        data: {
          label: 'Health Check',
          description: 'GET /health endpoint',
          nodeType: 'action-http',
          category: 'action',
          icon: 'Globe',
          config: {
            method: 'GET',
            url: 'https://api.yourservice.com/health',
            timeout: 10,
          },
          isConfigured: false,
        },
      },
      {
        id: 'logic-1',
        type: 'workflowNode',
        position: { x: 250, y: 310 },
        data: {
          label: 'Check Status',
          description: 'Is service healthy?',
          nodeType: 'logic-condition',
          category: 'logic',
          icon: 'GitBranch',
          config: {
            field: 'response.status',
            operator: 'equals',
            value: '200',
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-2',
        type: 'workflowNode',
        position: { x: 400, y: 440 },
        data: {
          label: 'Send Alert',
          description: 'Notify on failure',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'send-alert',
            agentSelector: 'any',
            timeout: 30,
          },
          isConfigured: false,
        },
      },
    ] as WorkflowNode[],
    edges: [
      {
        id: 'edge-1',
        source: 'trigger-1',
        target: 'action-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-2',
        source: 'action-1',
        target: 'logic-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-3',
        source: 'logic-1',
        sourceHandle: 'false',
        target: 'action-2',
        type: 'smoothstep',
        data: { label: 'Unhealthy' },
      },
    ] as WorkflowEdge[],
  },
  {
    id: 'batch-processor',
    name: 'Batch Processor',
    description: 'Process items from an array in batches with parallel execution support.',
    category: 'data-processing',
    icon: 'Filter',
    tags: ['batch', 'loop', 'parallel', 'processing'],
    nodes: [
      {
        id: 'trigger-1',
        type: 'workflowNode',
        position: { x: 250, y: 50 },
        data: {
          label: 'Webhook Trigger',
          description: 'Receive batch data',
          nodeType: 'trigger-webhook',
          category: 'trigger',
          icon: 'Webhook',
          config: {
            method: 'POST',
            path: '/webhook/batch',
            authentication: 'none',
          },
          isConfigured: true,
        },
      },
      {
        id: 'transform-1',
        type: 'workflowNode',
        position: { x: 250, y: 180 },
        data: {
          label: 'Filter Items',
          description: 'Filter valid items',
          nodeType: 'transform-filter',
          category: 'transform',
          icon: 'Filter',
          config: {
            arrayField: 'body.items',
            field: 'status',
            operator: 'equals',
            value: 'pending',
          },
          isConfigured: false,
        },
      },
      {
        id: 'logic-1',
        type: 'workflowNode',
        position: { x: 250, y: 310 },
        data: {
          label: 'Loop Items',
          description: 'Process each item',
          nodeType: 'logic-loop',
          category: 'logic',
          icon: 'Repeat',
          config: {
            arrayField: 'filteredItems',
            batchSize: 5,
            parallelExecution: true,
            maxIterations: 100,
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-1',
        type: 'workflowNode',
        position: { x: 250, y: 440 },
        data: {
          label: 'Process Item',
          description: 'Execute for each item',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'process-item',
            agentSelector: 'any',
            timeout: 60,
          },
          isConfigured: false,
        },
      },
      {
        id: 'transform-2',
        type: 'workflowNode',
        position: { x: 250, y: 570 },
        data: {
          label: 'Aggregate Results',
          description: 'Combine all results',
          nodeType: 'transform-aggregate',
          category: 'transform',
          icon: 'Layers',
          config: {
            arrayField: 'processedItems',
            operation: 'count',
            outputField: 'summary',
          },
          isConfigured: true,
        },
      },
    ] as WorkflowNode[],
    edges: [
      {
        id: 'edge-1',
        source: 'trigger-1',
        target: 'transform-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-2',
        source: 'transform-1',
        target: 'logic-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-3',
        source: 'logic-1',
        target: 'action-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-4',
        source: 'action-1',
        target: 'transform-2',
        type: 'smoothstep',
      },
    ] as WorkflowEdge[],
  },
  {
    id: 'deploy-pipeline',
    name: 'Deployment Pipeline',
    description: 'Simple CI/CD pipeline with build, test, and deploy stages.',
    category: 'devops',
    icon: 'RefreshCw',
    tags: ['deploy', 'ci', 'cd', 'devops', 'pipeline'],
    nodes: [
      {
        id: 'trigger-1',
        type: 'workflowNode',
        position: { x: 250, y: 50 },
        data: {
          label: 'Webhook Trigger',
          description: 'Git push webhook',
          nodeType: 'trigger-webhook',
          category: 'trigger',
          icon: 'Webhook',
          config: {
            method: 'POST',
            path: '/webhook/deploy',
            authentication: 'header',
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-1',
        type: 'workflowNode',
        position: { x: 250, y: 180 },
        data: {
          label: 'Build',
          description: 'Build the application',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'build',
            agentSelector: 'tag',
            agentTag: 'build-server',
            timeout: 600,
            retryCount: 1,
          },
          isConfigured: false,
        },
      },
      {
        id: 'action-2',
        type: 'workflowNode',
        position: { x: 250, y: 310 },
        data: {
          label: 'Test',
          description: 'Run test suite',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'test',
            agentSelector: 'tag',
            agentTag: 'test-server',
            timeout: 900,
          },
          isConfigured: false,
        },
      },
      {
        id: 'logic-1',
        type: 'workflowNode',
        position: { x: 250, y: 440 },
        data: {
          label: 'Tests Passed?',
          description: 'Check test results',
          nodeType: 'logic-condition',
          category: 'logic',
          icon: 'GitBranch',
          config: {
            field: 'testResult.passed',
            operator: 'equals',
            value: 'true',
          },
          isConfigured: true,
        },
      },
      {
        id: 'action-3',
        type: 'workflowNode',
        position: { x: 100, y: 570 },
        data: {
          label: 'Deploy',
          description: 'Deploy to production',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'deploy',
            agentSelector: 'tag',
            agentTag: 'production',
            timeout: 300,
          },
          isConfigured: false,
        },
      },
      {
        id: 'action-4',
        type: 'workflowNode',
        position: { x: 400, y: 570 },
        data: {
          label: 'Notify Failure',
          description: 'Send failure alert',
          nodeType: 'action-job',
          category: 'action',
          icon: 'Zap',
          config: {
            jobType: 'notify',
            agentSelector: 'any',
            timeout: 30,
          },
          isConfigured: false,
        },
      },
    ] as WorkflowNode[],
    edges: [
      {
        id: 'edge-1',
        source: 'trigger-1',
        target: 'action-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-2',
        source: 'action-1',
        target: 'action-2',
        type: 'smoothstep',
      },
      {
        id: 'edge-3',
        source: 'action-2',
        target: 'logic-1',
        type: 'smoothstep',
      },
      {
        id: 'edge-4',
        source: 'logic-1',
        sourceHandle: 'true',
        target: 'action-3',
        type: 'smoothstep',
        data: { label: 'Passed' },
      },
      {
        id: 'edge-5',
        source: 'logic-1',
        sourceHandle: 'false',
        target: 'action-4',
        type: 'smoothstep',
        data: { label: 'Failed' },
      },
    ] as WorkflowEdge[],
  },
]

interface WorkflowTemplatesDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSelectTemplate: (template: WorkflowTemplate) => void
}

export function WorkflowTemplatesDialog({
  open,
  onOpenChange,
  onSelectTemplate,
}: WorkflowTemplatesDialogProps) {
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const [selectedTemplate, setSelectedTemplate] = useState<WorkflowTemplate | null>(null)

  // Filter templates
  const filteredTemplates = useMemo(() => {
    return WORKFLOW_TEMPLATES.filter((template) => {
      const matchesSearch =
        !searchQuery ||
        template.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        template.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
        template.tags.some((tag) => tag.toLowerCase().includes(searchQuery.toLowerCase()))

      const matchesCategory = !selectedCategory || template.category === selectedCategory

      return matchesSearch && matchesCategory
    })
  }, [searchQuery, selectedCategory])

  // Group templates by category
  const templatesByCategory = useMemo(() => {
    const grouped: Record<string, WorkflowTemplate[]> = {}
    filteredTemplates.forEach((template) => {
      if (!grouped[template.category]) {
        grouped[template.category] = []
      }
      grouped[template.category].push(template)
    })
    return grouped
  }, [filteredTemplates])

  const handleSelectTemplate = () => {
    if (selectedTemplate) {
      onSelectTemplate(selectedTemplate)
      onOpenChange(false)
      setSelectedTemplate(null)
      setSearchQuery('')
      setSelectedCategory(null)
    }
  }

  const handleClose = () => {
    onOpenChange(false)
    setSelectedTemplate(null)
    setSearchQuery('')
    setSelectedCategory(null)
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-4xl h-[80vh] flex flex-col p-0">
        <DialogHeader className="px-6 pt-6 pb-4 border-b">
          <div className="flex items-center justify-between">
            <div>
              <DialogTitle className="flex items-center gap-2 text-xl">
                <Workflow className="w-5 h-5 text-primary" />
                Workflow Templates
              </DialogTitle>
              <DialogDescription className="mt-1">
                Start with a pre-built template and customize it for your needs
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="flex-1 flex overflow-hidden">
          {/* Sidebar with filters */}
          <div className="w-64 border-r bg-muted/30 p-4 flex flex-col">
            {/* Search */}
            <div className="relative mb-4">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search templates..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-8"
              />
            </div>

            {/* Categories */}
            <div className="space-y-1">
              <button
                onClick={() => setSelectedCategory(null)}
                className={cn(
                  'w-full flex items-center gap-2 px-3 py-2 rounded-md text-sm transition-colors',
                  !selectedCategory
                    ? 'bg-primary text-primary-foreground'
                    : 'hover:bg-muted'
                )}
              >
                <Workflow className="w-4 h-4" />
                All Templates
                <span className="ml-auto text-xs opacity-70">{WORKFLOW_TEMPLATES.length}</span>
              </button>

              {Object.entries(TEMPLATE_CATEGORIES).map(([key, category]) => {
                const count = WORKFLOW_TEMPLATES.filter((t) => t.category === key).length
                const Icon = category.icon
                return (
                  <button
                    key={key}
                    onClick={() => setSelectedCategory(key)}
                    className={cn(
                      'w-full flex items-center gap-2 px-3 py-2 rounded-md text-sm transition-colors',
                      selectedCategory === key
                        ? 'bg-primary text-primary-foreground'
                        : 'hover:bg-muted'
                    )}
                  >
                    <Icon className="w-4 h-4" />
                    {category.label}
                    <span className="ml-auto text-xs opacity-70">{count}</span>
                  </button>
                )
              })}
            </div>
          </div>

          {/* Template list and preview */}
          <div className="flex-1 flex">
            {/* Template list */}
            <ScrollArea className="flex-1 border-r">
              <div className="p-4 space-y-6">
                {Object.entries(templatesByCategory).length === 0 ? (
                  <div className="text-center py-12 text-muted-foreground">
                    <Search className="w-12 h-12 mx-auto mb-4 opacity-30" />
                    <p className="text-lg font-medium">No templates found</p>
                    <p className="text-sm">Try adjusting your search or filters</p>
                  </div>
                ) : (
                  Object.entries(templatesByCategory).map(([category, templates]) => {
                    const categoryInfo = TEMPLATE_CATEGORIES[category]
                    return (
                      <div key={category}>
                        <div className="flex items-center gap-2 mb-3">
                          <div
                            className={cn('w-6 h-6 rounded flex items-center justify-center', categoryInfo.color)}
                          >
                            <categoryInfo.icon className="w-3.5 h-3.5 text-white" />
                          </div>
                          <h3 className="font-medium">{categoryInfo.label}</h3>
                        </div>

                        <div className="grid gap-2">
                          {templates.map((template) => {
                            const Icon = ICON_MAP[template.icon] || Workflow
                            const isSelected = selectedTemplate?.id === template.id
                            return (
                              <button
                                key={template.id}
                                onClick={() => setSelectedTemplate(template)}
                                className={cn(
                                  'w-full text-left p-3 rounded-lg border transition-all',
                                  isSelected
                                    ? 'border-primary bg-primary/5 ring-1 ring-primary'
                                    : 'border-transparent bg-muted/50 hover:bg-muted hover:border-muted-foreground/20'
                                )}
                              >
                                <div className="flex items-start gap-3">
                                  <div
                                    className={cn(
                                      'w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0',
                                      categoryInfo.color
                                    )}
                                  >
                                    <Icon className="w-5 h-5 text-white" />
                                  </div>
                                  <div className="flex-1 min-w-0">
                                    <div className="flex items-center gap-2">
                                      <h4 className="font-medium truncate">{template.name}</h4>
                                      {isSelected && (
                                        <CheckCircle className="w-4 h-4 text-primary flex-shrink-0" />
                                      )}
                                    </div>
                                    <p className="text-sm text-muted-foreground line-clamp-2 mt-0.5">
                                      {template.description}
                                    </p>
                                  </div>
                                </div>
                              </button>
                            )
                          })}
                        </div>
                      </div>
                    )
                  })
                )}
              </div>
            </ScrollArea>

            {/* Template preview */}
            <div className="w-80 bg-muted/30 p-4 flex flex-col">
              {selectedTemplate ? (
                <>
                  <div className="flex-1">
                    <div className="flex items-center gap-3 mb-4">
                      {(() => {
                        const Icon = ICON_MAP[selectedTemplate.icon] || Workflow
                        const categoryInfo = TEMPLATE_CATEGORIES[selectedTemplate.category]
                        return (
                          <div
                            className={cn(
                              'w-12 h-12 rounded-lg flex items-center justify-center',
                              categoryInfo.color
                            )}
                          >
                            <Icon className="w-6 h-6 text-white" />
                          </div>
                        )
                      })()}
                      <div>
                        <h3 className="font-semibold">{selectedTemplate.name}</h3>
                        <p className="text-sm text-muted-foreground">
                          {TEMPLATE_CATEGORIES[selectedTemplate.category]?.label}
                        </p>
                      </div>
                    </div>

                    <p className="text-sm text-muted-foreground mb-4">
                      {selectedTemplate.description}
                    </p>

                    {/* Tags */}
                    <div className="flex flex-wrap gap-1 mb-4">
                      {selectedTemplate.tags.map((tag) => (
                        <Badge key={tag} variant="secondary" className="text-xs">
                          {tag}
                        </Badge>
                      ))}
                    </div>

                    {/* Stats */}
                    <div className="bg-background rounded-lg p-3 space-y-2 mb-4">
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Nodes</span>
                        <span className="font-medium">{selectedTemplate.nodes.length}</span>
                      </div>
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Connections</span>
                        <span className="font-medium">{selectedTemplate.edges.length}</span>
                      </div>
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Trigger</span>
                        <span className="font-medium">
                          {selectedTemplate.nodes.find((n) => n.data.category === 'trigger')?.data.label || 'None'}
                        </span>
                      </div>
                    </div>

                    {/* Node list preview */}
                    <div className="space-y-1">
                      <h4 className="text-sm font-medium mb-2">Workflow Steps</h4>
                      {selectedTemplate.nodes.map((node, index) => (
                        <div
                          key={node.id}
                          className="flex items-center gap-2 text-sm py-1"
                        >
                          <span className="w-5 h-5 rounded bg-muted flex items-center justify-center text-xs">
                            {index + 1}
                          </span>
                          <span className="truncate">{node.data.label}</span>
                        </div>
                      ))}
                    </div>
                  </div>

                  <Button onClick={handleSelectTemplate} className="w-full mt-4">
                    Use This Template
                    <ArrowRight className="w-4 h-4 ml-2" />
                  </Button>
                </>
              ) : (
                <div className="flex-1 flex flex-col items-center justify-center text-center text-muted-foreground">
                  <Workflow className="w-16 h-16 mb-4 opacity-30" />
                  <h3 className="font-medium mb-1">Select a Template</h3>
                  <p className="text-sm">
                    Click on a template to see its details and preview
                  </p>
                </div>
              )}
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}
