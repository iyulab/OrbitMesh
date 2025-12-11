import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Command } from 'cmdk'
import {
  Server,
  PlayCircle,
  GitBranch,
  Settings,
  LayoutDashboard,
  Search,
  Plus,
  ArrowRight,
} from 'lucide-react'
import { getAgents, getJobs, getWorkflows } from '@/api/client'
import { cn } from '@/lib/utils'

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')

  const { data: agents = [] } = useQuery({
    queryKey: ['agents'],
    queryFn: getAgents,
    enabled: open,
  })

  const { data: jobs = [] } = useQuery({
    queryKey: ['jobs'],
    queryFn: () => getJobs(),
    enabled: open,
  })

  const { data: workflows = [] } = useQuery({
    queryKey: ['workflows'],
    queryFn: getWorkflows,
    enabled: open,
  })

  const runCommand = useCallback(
    (command: () => void) => {
      onOpenChange(false)
      command()
    },
    [onOpenChange]
  )

  // Reset search when opened
  useEffect(() => {
    if (open) {
      setSearch('')
    }
  }, [open])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/50 backdrop-blur-sm"
        onClick={() => onOpenChange(false)}
      />

      {/* Command Dialog */}
      <div className="fixed left-1/2 top-1/4 -translate-x-1/2 w-full max-w-xl">
        <Command
          className="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-2xl overflow-hidden"
          shouldFilter={true}
        >
          <div className="flex items-center border-b border-slate-200 dark:border-slate-700 px-3">
            <Search className="w-4 h-4 text-slate-400 mr-2" />
            <Command.Input
              value={search}
              onValueChange={setSearch}
              placeholder="Search agents, jobs, workflows, or type a command..."
              className="flex-1 h-12 bg-transparent text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none"
            />
            <kbd className="hidden md:inline-flex h-5 select-none items-center gap-1 rounded border border-slate-200 dark:border-slate-600 bg-slate-100 dark:bg-slate-700 px-1.5 font-mono text-[10px] font-medium text-slate-500">
              ESC
            </kbd>
          </div>

          <Command.List className="max-h-[400px] overflow-y-auto p-2">
            <Command.Empty className="py-6 text-center text-sm text-slate-500">
              No results found.
            </Command.Empty>

            {/* Navigation */}
            <Command.Group heading="Navigation" className="px-2 py-1.5 text-xs font-medium text-slate-500">
              <CommandItem
                onSelect={() => runCommand(() => navigate('/'))}
                icon={LayoutDashboard}
              >
                Go to Dashboard
              </CommandItem>
              <CommandItem
                onSelect={() => runCommand(() => navigate('/agents'))}
                icon={Server}
              >
                Go to Agents
              </CommandItem>
              <CommandItem
                onSelect={() => runCommand(() => navigate('/jobs'))}
                icon={PlayCircle}
              >
                Go to Jobs
              </CommandItem>
              <CommandItem
                onSelect={() => runCommand(() => navigate('/workflows'))}
                icon={GitBranch}
              >
                Go to Workflows
              </CommandItem>
              <CommandItem
                onSelect={() => runCommand(() => navigate('/settings'))}
                icon={Settings}
              >
                Go to Settings
              </CommandItem>
            </Command.Group>

            {/* Quick Actions */}
            <Command.Group heading="Quick Actions" className="px-2 py-1.5 text-xs font-medium text-slate-500">
              <CommandItem
                onSelect={() => runCommand(() => navigate('/workflows/new'))}
                icon={Plus}
              >
                Create New Workflow
              </CommandItem>
            </Command.Group>

            {/* Agents */}
            {agents.length > 0 && (
              <Command.Group heading="Agents" className="px-2 py-1.5 text-xs font-medium text-slate-500">
                {agents.slice(0, 5).map((agent) => (
                  <CommandItem
                    key={agent.id}
                    onSelect={() => runCommand(() => navigate(`/agents/${agent.id}`))}
                    icon={Server}
                  >
                    <div className="flex items-center justify-between w-full">
                      <span>{agent.name}</span>
                      <span
                        className={cn(
                          'text-xs px-1.5 py-0.5 rounded',
                          agent.status === 'Ready'
                            ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                            : agent.status === 'Disconnected'
                              ? 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                              : 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400'
                        )}
                      >
                        {agent.status}
                      </span>
                    </div>
                  </CommandItem>
                ))}
                {agents.length > 5 && (
                  <CommandItem
                    onSelect={() => runCommand(() => navigate('/agents'))}
                    icon={ArrowRight}
                  >
                    View all {agents.length} agents
                  </CommandItem>
                )}
              </Command.Group>
            )}

            {/* Jobs */}
            {jobs.length > 0 && (
              <Command.Group heading="Recent Jobs" className="px-2 py-1.5 text-xs font-medium text-slate-500">
                {jobs.slice(0, 5).map((job) => (
                  <CommandItem
                    key={job.id}
                    onSelect={() => runCommand(() => navigate(`/jobs/${job.id}`))}
                    icon={PlayCircle}
                  >
                    <div className="flex items-center justify-between w-full">
                      <span className="truncate max-w-[200px]">{job.command}</span>
                      <span
                        className={cn(
                          'text-xs px-1.5 py-0.5 rounded',
                          job.status === 'Completed'
                            ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                            : job.status === 'Failed'
                              ? 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                              : job.status === 'Running'
                                ? 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400'
                                : 'bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-300'
                        )}
                      >
                        {job.status}
                      </span>
                    </div>
                  </CommandItem>
                ))}
                {jobs.length > 5 && (
                  <CommandItem
                    onSelect={() => runCommand(() => navigate('/jobs'))}
                    icon={ArrowRight}
                  >
                    View all {jobs.length} jobs
                  </CommandItem>
                )}
              </Command.Group>
            )}

            {/* Workflows */}
            {workflows.length > 0 && (
              <Command.Group heading="Workflows" className="px-2 py-1.5 text-xs font-medium text-slate-500">
                {workflows.slice(0, 5).map((workflow) => (
                  <CommandItem
                    key={workflow.id}
                    onSelect={() => runCommand(() => navigate(`/workflows/${workflow.id}/edit`))}
                    icon={GitBranch}
                  >
                    <div className="flex items-center justify-between w-full">
                      <span>{workflow.name}</span>
                      <span className="text-xs text-slate-500">v{workflow.version}</span>
                    </div>
                  </CommandItem>
                ))}
                {workflows.length > 5 && (
                  <CommandItem
                    onSelect={() => runCommand(() => navigate('/workflows'))}
                    icon={ArrowRight}
                  >
                    View all {workflows.length} workflows
                  </CommandItem>
                )}
              </Command.Group>
            )}
          </Command.List>

          {/* Footer */}
          <div className="flex items-center justify-between border-t border-slate-200 dark:border-slate-700 px-3 py-2 text-xs text-slate-500">
            <div className="flex items-center gap-4">
              <span className="flex items-center gap-1">
                <kbd className="px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-700">↑↓</kbd>
                Navigate
              </span>
              <span className="flex items-center gap-1">
                <kbd className="px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-700">↵</kbd>
                Select
              </span>
            </div>
            <span className="flex items-center gap-1">
              <kbd className="px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-700">Ctrl</kbd>
              <kbd className="px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-700">K</kbd>
              Toggle
            </span>
          </div>
        </Command>
      </div>
    </div>
  )
}

function CommandItem({
  children,
  onSelect,
  icon: Icon,
}: {
  children: React.ReactNode
  onSelect: () => void
  icon: React.ComponentType<{ className?: string }>
}) {
  return (
    <Command.Item
      onSelect={onSelect}
      className="flex items-center gap-2 px-2 py-2 rounded-lg text-sm text-slate-700 dark:text-slate-300 cursor-pointer aria-selected:bg-slate-100 dark:aria-selected:bg-slate-700"
    >
      <Icon className="w-4 h-4 text-slate-400" />
      <span className="flex-1">{children}</span>
    </Command.Item>
  )
}

// Hook to use command palette with keyboard shortcut
export function useCommandPalette() {
  const [open, setOpen] = useState(false)

  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault()
        setOpen((open) => !open)
      }
      if (e.key === 'Escape') {
        setOpen(false)
      }
    }

    document.addEventListener('keydown', down)
    return () => document.removeEventListener('keydown', down)
  }, [])

  return { open, setOpen }
}
