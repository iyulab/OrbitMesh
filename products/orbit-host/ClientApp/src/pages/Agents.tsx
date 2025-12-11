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
} from 'lucide-react'
import { getAgents, createApiToken, generateAgentCommand, generateDockerCommand } from '@/api/client'
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

function AddAgentDialog({
  open,
  onOpenChange,
  serverUrl
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  serverUrl: string
}) {
  const [tokenName, setTokenName] = useState('')
  const [agentName, setAgentName] = useState('')
  const [agentGroup, setAgentGroup] = useState('')
  const [generatedToken, setGeneratedToken] = useState<string | null>(null)
  const [copied, setCopied] = useState<'cli' | 'docker' | null>(null)

  const queryClient = useQueryClient()

  const createTokenMutation = useMutation({
    mutationFn: createApiToken,
    onSuccess: (data) => {
      setGeneratedToken(data.token || '')
      queryClient.invalidateQueries({ queryKey: ['tokens'] })
      toast.success('Token generated successfully')
    },
    onError: (error) => {
      toast.error('Failed to generate token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const handleGenerateToken = () => {
    createTokenMutation.mutate({
      name: tokenName || `agent-${Date.now()}`,
      scopes: ['agent:connect'],
    })
  }

  const cliCommand = generatedToken
    ? generateAgentCommand(serverUrl, generatedToken, {
        name: agentName || undefined,
        group: agentGroup || undefined,
      })
    : ''

  const dockerCommand = generatedToken
    ? generateDockerCommand(serverUrl, generatedToken, {
        name: agentName || undefined,
        group: agentGroup || undefined,
      })
    : ''

  const handleCopy = async (type: 'cli' | 'docker') => {
    const command = type === 'cli' ? cliCommand : dockerCommand
    try {
      await navigator.clipboard.writeText(command)
      setCopied(type)
      toast.success('Copied to clipboard')
      setTimeout(() => setCopied(null), 2000)
    } catch {
      toast.error('Failed to copy to clipboard')
    }
  }

  const handleClose = () => {
    setTokenName('')
    setAgentName('')
    setAgentGroup('')
    setGeneratedToken(null)
    setCopied(null)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && handleClose()}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add New Agent</DialogTitle>
          <DialogDescription>
            Generate a token and connection command for a new agent
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 py-4">
          {!generatedToken ? (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="token-name">Token Name</Label>
                <Input
                  id="token-name"
                  placeholder="my-agent-token"
                  value={tokenName}
                  onChange={(e) => setTokenName(e.target.value)}
                />
              </div>

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

              <Button
                onClick={handleGenerateToken}
                disabled={createTokenMutation.isPending}
                className="w-full"
              >
                {createTokenMutation.isPending ? 'Generating...' : 'Generate Token'}
              </Button>
            </div>
          ) : (
            <>
              <Alert className="border-yellow-200 bg-yellow-50 dark:border-yellow-500/20 dark:bg-yellow-500/10">
                <AlertDescription className="text-yellow-700 dark:text-yellow-400">
                  Copy this token now. It will not be shown again.
                </AlertDescription>
              </Alert>

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

              <div className="bg-slate-50 dark:bg-slate-900 rounded-lg p-4">
                <h3 className="text-sm font-medium text-slate-900 dark:text-white mb-2">Instructions</h3>
                <ol className="text-sm text-slate-600 dark:text-slate-400 space-y-2 list-decimal list-inside">
                  <li>Copy the command above</li>
                  <li>Run it on the machine where you want to deploy the agent</li>
                  <li>The agent will automatically connect and appear in this list</li>
                </ol>
              </div>
            </>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            {generatedToken ? 'Done' : 'Cancel'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default function Agents() {
  const [showAddDialog, setShowAddDialog] = useState(false)
  const queryClient = useQueryClient()

  const { data: agents = [], isLoading } = useQuery({
    queryKey: ['agents'],
    queryFn: getAgents,
  })

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
            onClick={() => queryClient.invalidateQueries({ queryKey: ['agents'] })}
          >
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
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
                      <Link to={`/agents/${agent.id}`}>
                        <Button variant="ghost" size="sm">
                          <ChevronRight className="w-4 h-4" />
                        </Button>
                      </Link>
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
    </div>
  )
}
