import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Server,
  Plus,
  Copy,
  Check,
  Terminal,
  Container,
  RefreshCw,
  ChevronRight,
  Ticket,
  FolderSync,
} from 'lucide-react'
import { getAgents, getBootstrapToken, regenerateBootstrapToken } from '@/api/client'
import type { Agent } from '@/types'
import { AgentStatusBadge } from '@/components/ui/status-badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { toast } from '@/components/ui/sonner'
import { AgentFileSyncDialog } from '@/components/deployment/AgentFileSyncDialog'

function generateBootstrapAgentCommand(serverUrl: string, token: string, options?: {
  name?: string
  group?: string
}): string {
  const args = [
    'orbit-agent',
    `--server-url "${serverUrl}"`,
    `--bootstrap-token "${token}"`,
  ]

  if (options?.name) {
    args.push(`--name "${options.name}"`)
  }
  if (options?.group) {
    args.push(`--group "${options.group}"`)
  }

  return args.join(' \\\n  ')
}

function generateBootstrapDockerCommand(serverUrl: string, token: string, options?: {
  name?: string
  group?: string
  image?: string
}): string {
  const image = options?.image || 'orbitmesh/agent:latest'
  const envVars = [
    `-e ORBIT_SERVER_URL="${serverUrl}"`,
    `-e ORBIT_BOOTSTRAP_TOKEN="${token}"`,
  ]

  if (options?.name) {
    envVars.push(`-e ORBIT_AGENT_NAME="${options.name}"`)
  }
  if (options?.group) {
    envVars.push(`-e ORBIT_AGENT_GROUP="${options.group}"`)
  }

  return `docker run -d \\\n  ${envVars.join(' \\\n  ')} \\\n  ${image}`
}

function AddAgentDialog({
  open,
  onOpenChange,
  serverUrl
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  serverUrl: string
}) {
  const [agentName, setAgentName] = useState('')
  const [agentGroup, setAgentGroup] = useState('')
  const [copied, setCopied] = useState<'cli' | 'docker' | 'token' | null>(null)

  const queryClient = useQueryClient()

  const { data: bootstrapToken, isLoading } = useQuery({
    queryKey: ['bootstrapToken'],
    queryFn: getBootstrapToken,
  })

  const regenerateMutation = useMutation({
    mutationFn: regenerateBootstrapToken,
    onSuccess: (data) => {
      queryClient.setQueryData(['bootstrapToken'], data)
      toast.success('Bootstrap token regenerated')
    },
    onError: (error) => {
      toast.error('Failed to regenerate token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  // Use the token value if available (just regenerated), otherwise show placeholder
  const tokenValue = bootstrapToken?.token || '(Click "Regenerate" to get token value)'
  const hasToken = !!bootstrapToken?.token

  const cliCommand = hasToken
    ? generateBootstrapAgentCommand(serverUrl, bootstrapToken.token!, {
        name: agentName || undefined,
        group: agentGroup || undefined,
      })
    : ''

  const dockerCommand = hasToken
    ? generateBootstrapDockerCommand(serverUrl, bootstrapToken.token!, {
        name: agentName || undefined,
        group: agentGroup || undefined,
      })
    : ''

  const handleCopy = async (type: 'cli' | 'docker' | 'token') => {
    let text = ''
    if (type === 'cli') text = cliCommand
    else if (type === 'docker') text = dockerCommand
    else if (type === 'token' && bootstrapToken?.token) text = bootstrapToken.token

    if (!text) {
      toast.error('No token available. Please regenerate first.')
      return
    }

    try {
      await navigator.clipboard.writeText(text)
      setCopied(type)
      toast.success('Copied to clipboard')
      setTimeout(() => setCopied(null), 2000)
    } catch {
      toast.error('Failed to copy to clipboard')
    }
  }

  const handleClose = () => {
    setAgentName('')
    setAgentGroup('')
    setCopied(null)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && handleClose()}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add New Agent</DialogTitle>
          <DialogDescription>
            Use the Bootstrap Token to enroll new agents securely (TOFU)
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 py-4">
          {isLoading ? (
            <div className="text-center py-8">
              <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
              <p className="text-slate-500">Loading bootstrap token...</p>
            </div>
          ) : (
            <>
              {/* Bootstrap Token Section */}
              <div className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg border border-slate-200 dark:border-slate-700">
                <div className="flex items-center gap-2 mb-3">
                  <Ticket className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
                  <h3 className="text-sm font-medium text-slate-900 dark:text-white">Bootstrap Token</h3>
                  {!bootstrapToken?.isEnabled && (
                    <span className="px-2 py-0.5 bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400 text-xs rounded">
                      Disabled
                    </span>
                  )}
                </div>

                {!bootstrapToken?.isEnabled ? (
                  <Alert className="border-red-200 bg-red-50 dark:border-red-500/20 dark:bg-red-500/10">
                    <AlertDescription className="text-red-700 dark:text-red-400">
                      Bootstrap token is disabled. Enable it in Settings to allow new agent enrollment.
                    </AlertDescription>
                  </Alert>
                ) : hasToken ? (
                  <>
                    <Alert className="border-yellow-200 bg-yellow-50 dark:border-yellow-500/20 dark:bg-yellow-500/10 mb-3">
                      <AlertDescription className="text-yellow-700 dark:text-yellow-400">
                        Copy this token now. It will not be shown again after closing this dialog.
                      </AlertDescription>
                    </Alert>
                    <div className="relative">
                      <Input
                        readOnly
                        value={tokenValue}
                        className="pr-12 font-mono text-sm"
                      />
                      <Button
                        variant="ghost"
                        size="icon"
                        className="absolute right-1 top-1/2 -translate-y-1/2"
                        onClick={() => handleCopy('token')}
                      >
                        {copied === 'token' ? (
                          <Check className="w-4 h-4 text-green-600" />
                        ) : (
                          <Copy className="w-4 h-4" />
                        )}
                      </Button>
                    </div>
                  </>
                ) : (
                  <div className="text-center py-4">
                    <p className="text-sm text-slate-500 dark:text-slate-400 mb-3">
                      Click regenerate to get the bootstrap token value
                    </p>
                    <Button
                      onClick={() => regenerateMutation.mutate()}
                      disabled={regenerateMutation.isPending}
                    >
                      <RefreshCw className={`w-4 h-4 mr-2 ${regenerateMutation.isPending ? 'animate-spin' : ''}`} />
                      {regenerateMutation.isPending ? 'Regenerating...' : 'Regenerate Token'}
                    </Button>
                  </div>
                )}
              </div>

              {/* Agent Options */}
              {hasToken && (
                <>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="agent-name">Agent Name (optional)</Label>
                      <Input
                        id="agent-name"
                        placeholder="worker-01"
                        value={agentName}
                        onChange={(e) => setAgentName(e.target.value)}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="agent-group">Agent Group (optional)</Label>
                      <Input
                        id="agent-group"
                        placeholder="production"
                        value={agentGroup}
                        onChange={(e) => setAgentGroup(e.target.value)}
                      />
                    </div>
                  </div>

                  {/* Connection Commands */}
                  <Tabs defaultValue="cli">
                    <TabsList className="grid w-full grid-cols-2">
                      <TabsTrigger value="cli" className="flex items-center gap-2">
                        <Terminal className="w-4 h-4" />
                        CLI Command
                      </TabsTrigger>
                      <TabsTrigger value="docker" className="flex items-center gap-2">
                        <Container className="w-4 h-4" />
                        Docker Command
                      </TabsTrigger>
                    </TabsList>
                    <TabsContent value="cli" className="mt-4">
                      <div className="relative">
                        <pre className="bg-slate-100 dark:bg-slate-900 rounded-lg p-4 text-sm text-slate-700 dark:text-slate-300 overflow-x-auto">
                          {cliCommand}
                        </pre>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="absolute top-2 right-2"
                          onClick={() => handleCopy('cli')}
                        >
                          {copied === 'cli' ? (
                            <Check className="w-4 h-4 text-green-600" />
                          ) : (
                            <Copy className="w-4 h-4" />
                          )}
                        </Button>
                      </div>
                    </TabsContent>
                    <TabsContent value="docker" className="mt-4">
                      <div className="relative">
                        <pre className="bg-slate-100 dark:bg-slate-900 rounded-lg p-4 text-sm text-slate-700 dark:text-slate-300 overflow-x-auto">
                          {dockerCommand}
                        </pre>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="absolute top-2 right-2"
                          onClick={() => handleCopy('docker')}
                        >
                          {copied === 'docker' ? (
                            <Check className="w-4 h-4 text-green-600" />
                          ) : (
                            <Copy className="w-4 h-4" />
                          )}
                        </Button>
                      </div>
                    </TabsContent>
                  </Tabs>

                  {/* Instructions */}
                  <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4 border border-blue-200 dark:border-blue-800">
                    <h3 className="text-sm font-medium text-blue-900 dark:text-blue-100 mb-2">How TOFU Enrollment Works</h3>
                    <ol className="text-sm text-blue-700 dark:text-blue-300 space-y-2 list-decimal list-inside">
                      <li>Agent connects using the bootstrap token</li>
                      <li>Server issues a certificate for the agent</li>
                      <li>Agent stores the certificate for future connections</li>
                      <li>All subsequent connections use certificate authentication</li>
                    </ol>
                    {bootstrapToken?.autoApprove && (
                      <p className="mt-3 text-xs text-blue-600 dark:text-blue-400">
                        ✓ Auto-approve is enabled - agents will be enrolled automatically
                      </p>
                    )}
                  </div>
                </>
              )}
            </>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            Done
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default function Agents() {
  const [showAddDialog, setShowAddDialog] = useState(false)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [fileSyncAgent, setFileSyncAgent] = useState<Agent | null>(null)
  const queryClient = useQueryClient()

  const { data: agents = [], isLoading } = useQuery({
    queryKey: ['agents'],
    queryFn: getAgents,
  })

  const handleRefresh = async () => {
    setIsRefreshing(true)
    try {
      const data = await getAgents()
      queryClient.setQueryData(['agents'], data)
    } catch (error) {
      console.error('Failed to refresh agents:', error)
    } finally {
      setIsRefreshing(false)
    }
  }

  const isFetching = isLoading || isRefreshing

  const serverUrl = typeof window !== 'undefined'
    ? `${window.location.protocol}//${window.location.host}`
    : 'http://localhost:5000'

  const agentsByStatus = {
    ready: agents.filter(a => a.status === 'Ready'),
    busy: agents.filter(a => a.status === 'Running'),
    disconnected: agents.filter(a => a.status === 'Disconnected'),
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Agents</h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Manage your distributed agents</p>
        </div>
        <div className="flex gap-3">
          <Button
            variant="outline"
            onClick={handleRefresh}
            disabled={isFetching}
          >
            <RefreshCw className={`w-4 h-4 mr-2 ${isFetching ? 'animate-spin' : ''}`} />
            {isFetching ? 'Refreshing...' : 'Refresh'}
          </Button>
          <Button onClick={() => setShowAddDialog(true)}>
            <Plus className="w-4 h-4 mr-2" />
            Add Agent
          </Button>
        </div>
      </div>

      {/* Status Summary */}
      <div className="grid grid-cols-3 gap-4">
        <div className="card">
          <div className="flex items-center gap-3">
            <div className="w-3 h-3 bg-green-500 rounded-full" />
            <span className="text-slate-600 dark:text-slate-400">Ready</span>
            <span className="text-slate-900 dark:text-white font-bold ml-auto">{agentsByStatus.ready.length}</span>
          </div>
        </div>
        <div className="card">
          <div className="flex items-center gap-3">
            <div className="w-3 h-3 bg-yellow-500 rounded-full" />
            <span className="text-slate-600 dark:text-slate-400">Busy</span>
            <span className="text-slate-900 dark:text-white font-bold ml-auto">{agentsByStatus.busy.length}</span>
          </div>
        </div>
        <div className="card">
          <div className="flex items-center gap-3">
            <div className="w-3 h-3 bg-red-500 rounded-full" />
            <span className="text-slate-600 dark:text-slate-400">Disconnected</span>
            <span className="text-slate-900 dark:text-white font-bold ml-auto">{agentsByStatus.disconnected.length}</span>
          </div>
        </div>
      </div>

      {/* Agents List */}
      <div className="card">
        {isLoading ? (
          <div className="text-center py-8">
            <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
            <p className="text-slate-500 dark:text-slate-400">Loading agents...</p>
          </div>
        ) : agents.length === 0 ? (
          <div className="text-center py-12">
            <Server className="w-12 h-12 text-slate-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">No agents connected</h3>
            <p className="text-slate-500 dark:text-slate-400 mb-4">
              Add your first agent to start distributing work
            </p>
            <Button onClick={() => setShowAddDialog(true)}>
              Add Agent
            </Button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="table-header">
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Name</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Status</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Group</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Capabilities</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Last Heartbeat</th>
                  <th className="text-right py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400"></th>
                </tr>
              </thead>
              <tbody>
                {agents.map((agent) => (
                  <tr key={agent.id} className="table-row hover:bg-slate-50 dark:hover:bg-slate-800/50">
                    <td className="py-3 px-4">
                      <div className="flex items-center gap-3">
                        <Server className="w-5 h-5 text-slate-400" />
                        <div>
                          <p className="text-slate-900 dark:text-white font-medium">{agent.name}</p>
                          <p className="text-xs text-slate-500 dark:text-slate-500 font-mono">{agent.id}</p>
                        </div>
                      </div>
                    </td>
                    <td className="py-3 px-4">
                      <AgentStatusBadge status={agent.status} />
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-slate-600 dark:text-slate-300">{agent.group || '-'}</span>
                    </td>
                    <td className="py-3 px-4">
                      <div className="flex flex-wrap gap-1">
                        {agent.capabilities.slice(0, 3).map((cap) => (
                          <span
                            key={cap.name}
                            className="px-2 py-0.5 bg-slate-200 dark:bg-slate-700 rounded text-xs text-slate-700 dark:text-slate-300"
                          >
                            {cap.name}
                          </span>
                        ))}
                        {agent.capabilities.length > 3 && (
                          <span className="px-2 py-0.5 bg-slate-200 dark:bg-slate-700 rounded text-xs text-slate-500 dark:text-slate-400">
                            +{agent.capabilities.length - 3}
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-sm text-slate-500 dark:text-slate-400">
                        {agent.lastHeartbeat
                          ? new Date(agent.lastHeartbeat).toLocaleString()
                          : '-'}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setFileSyncAgent(agent)}
                          disabled={agent.status === 'Disconnected'}
                          title="Configure File Sync"
                        >
                          <FolderSync className="w-4 h-4" />
                        </Button>
                        <Link to={`/agents/${agent.id}`}>
                          <Button variant="ghost" size="sm">
                            <ChevronRight className="w-4 h-4" />
                          </Button>
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Add Agent Dialog */}
      <AddAgentDialog
        open={showAddDialog}
        onOpenChange={setShowAddDialog}
        serverUrl={serverUrl}
      />

      {/* File Sync Dialog */}
      {fileSyncAgent && (
        <AgentFileSyncDialog
          open={!!fileSyncAgent}
          onOpenChange={(open) => !open && setFileSyncAgent(null)}
          agent={fileSyncAgent}
        />
      )}
    </div>
  )
}
