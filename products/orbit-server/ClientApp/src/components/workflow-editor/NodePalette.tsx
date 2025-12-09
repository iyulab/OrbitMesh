import { useState, useMemo } from 'react'
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
  Search,
  ChevronDown,
  ChevronRight,
  GripVertical,
} from 'lucide-react'
import { getNodesByCategory, CATEGORY_INFO } from './node-definitions'
import type { NodeDefinition, NodeCategory } from './types'
import { cn } from '@/lib/utils'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'

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

interface NodePaletteProps {
  className?: string
}

export function NodePalette({ className }: NodePaletteProps) {
  const [searchQuery, setSearchQuery] = useState('')
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(
    new Set(['trigger', 'action', 'logic', 'transform'])
  )

  // Get nodes grouped by category
  const nodesByCategory = useMemo(() => getNodesByCategory(), [])

  // Filter nodes based on search query
  const filteredNodesByCategory = useMemo(() => {
    if (!searchQuery.trim()) return nodesByCategory

    const query = searchQuery.toLowerCase()
    const filtered: Record<string, NodeDefinition[]> = {}

    Object.entries(nodesByCategory).forEach(([category, nodes]) => {
      const matchingNodes = nodes.filter(
        (node) =>
          node.label.toLowerCase().includes(query) ||
          node.description.toLowerCase().includes(query) ||
          node.type.toLowerCase().includes(query)
      )
      if (matchingNodes.length > 0) {
        filtered[category] = matchingNodes
      }
    })

    return filtered
  }, [nodesByCategory, searchQuery])

  // Toggle category expansion
  const toggleCategory = (category: string) => {
    setExpandedCategories((prev) => {
      const next = new Set(prev)
      if (next.has(category)) {
        next.delete(category)
      } else {
        next.add(category)
      }
      return next
    })
  }

  // Handle drag start
  const handleDragStart = (event: React.DragEvent, nodeType: string) => {
    event.dataTransfer.setData('application/workflow-node', nodeType)
    event.dataTransfer.effectAllowed = 'move'
  }

  return (
    <div className={cn('flex flex-col h-full bg-background border-r', className)}>
      {/* Header */}
      <div className="p-4 border-b">
        <h2 className="font-semibold mb-3">Node Palette</h2>
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search nodes..."
            value={searchQuery}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
            className="pl-8"
          />
        </div>
      </div>

      {/* Node Categories */}
      <ScrollArea className="flex-1">
        <div className="p-2">
          {Object.entries(filteredNodesByCategory).map(([category, nodes]) => {
            const categoryInfo = CATEGORY_INFO[category as NodeCategory]
            const isExpanded = expandedCategories.has(category)
            const CategoryIcon = ICON_MAP[categoryInfo.icon] || Zap

            return (
              <Collapsible
                key={category}
                open={isExpanded}
                onOpenChange={() => toggleCategory(category)}
                className="mb-2"
              >
                <CollapsibleTrigger className="flex items-center gap-2 w-full p-2 rounded-md hover:bg-muted transition-colors">
                  {isExpanded ? (
                    <ChevronDown className="w-4 h-4 text-muted-foreground" />
                  ) : (
                    <ChevronRight className="w-4 h-4 text-muted-foreground" />
                  )}
                  <div
                    className="w-6 h-6 rounded flex items-center justify-center"
                    style={{ backgroundColor: categoryInfo.color }}
                  >
                    <CategoryIcon className="w-3.5 h-3.5 text-white" />
                  </div>
                  <span className="font-medium text-sm">{categoryInfo.label}</span>
                  <span className="ml-auto text-xs text-muted-foreground">{nodes.length}</span>
                </CollapsibleTrigger>

                <CollapsibleContent className="pl-6 pr-2 pb-2">
                  <div className="space-y-1 mt-1">
                    {nodes.map((node) => (
                      <NodeItem
                        key={node.type}
                        node={node}
                        onDragStart={handleDragStart}
                      />
                    ))}
                  </div>
                </CollapsibleContent>
              </Collapsible>
            )
          })}

          {/* No results message */}
          {Object.keys(filteredNodesByCategory).length === 0 && (
            <div className="text-center py-8 text-muted-foreground">
              <p className="text-sm">No nodes found matching "{searchQuery}"</p>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Help Text */}
      <div className="p-3 border-t text-xs text-muted-foreground bg-muted/30">
        <p>Drag nodes onto the canvas to build your workflow</p>
      </div>
    </div>
  )
}

interface NodeItemProps {
  node: NodeDefinition
  onDragStart: (event: React.DragEvent, nodeType: string) => void
}

function NodeItem({ node, onDragStart }: NodeItemProps) {
  const Icon = ICON_MAP[node.icon] || Zap

  return (
    <div
      draggable
      onDragStart={(e) => onDragStart(e, node.type)}
      className={cn(
        'flex items-center gap-2 p-2 rounded-md border cursor-grab',
        'bg-background hover:bg-muted/50 hover:border-primary/30',
        'transition-all duration-150',
        'active:cursor-grabbing active:scale-[0.98]',
        'group'
      )}
    >
      <GripVertical className="w-3 h-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
      <div
        className="w-7 h-7 rounded flex items-center justify-center flex-shrink-0"
        style={{ backgroundColor: node.color + '20' }}
      >
        <span style={{ color: node.color }}>
          <Icon className="w-4 h-4" />
        </span>
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium truncate">{node.label}</p>
        <p className="text-xs text-muted-foreground truncate">{node.description}</p>
      </div>
    </div>
  )
}
