import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Server,
  Plus,
  Copy,
  Check,
  Terminal,
  Container,
  RefreshCw,
} from 'lucide-react'
import { getAgents, createApiToken, generateAgentCommand, generateDockerCommand } from '@/api/client'
import type { Agent } from '@/types'

function AgentStatusBadge({ status }: { status: Agent['status'] }) {
  const statusStyles: Record<string, string> = {
    Ready: 'status-ready',
    Running: 'status-busy',
    Disconnected: 'status-disconnected',
    Created: 'status-pending',
    Initializing: 'status-pending',
    Paused: 'bg-blue-500/20 text-blue-600 dark:text-blue-400',
    Stopping: 'bg-orange-500/20 text-orange-600 dark:text-orange-400',
    Stopped: 'bg-slate-500/20 text-slate-600 dark:text-slate-400',
    Faulted: 'bg-red-500/20 text-red-600 dark:text-red-400',
  }

  return (
    <span className={`status-badge ${statusStyles[status] || 'status-pending'}`}>
      {status}
    </span>
  )
}

function AddAgentModal({
  isOpen,
  onClose,
  serverUrl
}: {
  isOpen: boolean
  onClose: () => void
  serverUrl: string
}) {
  const [tokenName, setTokenName] = useState('')
  const [agentName, setAgentName] = useState('')
  const [agentGroup, setAgentGroup] = useState('')
  const [generatedToken, setGeneratedToken] = useState<string | null>(null)
  const [copied, setCopied] = useState<'cli' | 'docker' | null>(null)
  const [commandType, setCommandType] = useState<'cli' | 'docker'>('cli')

  const queryClient = useQueryClient()

  const createTokenMutation = useMutation({
    mutationFn: createApiToken,
    onSuccess: (data) => {
      setGeneratedToken(data.token || '')
      queryClient.invalidateQueries({ queryKey: ['tokens'] })
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
    await navigator.clipboard.writeText(command)
    setCopied(type)
    setTimeout(() => setCopied(null), 2000)
  }

  const handleClose = () => {
    setTokenName('')
    setAgentName('')
    setAgentGroup('')
    setGeneratedToken(null)
    setCopied(null)
    onClose()
  }

  if (!isOpen) return null

  return (
    <div className="modal-backdrop">
      <div className="modal-content w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-slate-200 dark:border-slate-700">
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Add New Agent</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            Generate a token and connection command for a new agent
          </p>
        </div>

        <div className="p-6 space-y-6">
          {/* Token Generation */}
          {!generatedToken ? (
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                  Token Name
                </label>
                <input
                  type="text"
                  className="input w-full"
                  placeholder="my-agent-token"
                  value={tokenName}
                  onChange={(e) => setTokenName(e.target.value)}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                    Agent Name (optional)
                  </label>
                  <input
                    type="text"
                    className="input w-full"
                    placeholder="worker-01"
                    value={agentName}
                    onChange={(e) => setAgentName(e.target.value)}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                    Agent Group (optional)
                  </label>
                  <input
                    type="text"
                    className="input w-full"
                    placeholder="production"
                    value={agentGroup}
                    onChange={(e) => setAgentGroup(e.target.value)}
                  />
                </div>
              </div>

              <button
                onClick={handleGenerateToken}
                disabled={createTokenMutation.isPending}
                className="btn-primary w-full"
              >
                {createTokenMutation.isPending ? 'Generating...' : 'Generate Token'}
              </button>
            </div>
          ) : (
            <>
              {/* Token Warning */}
              <div className="bg-yellow-50 dark:bg-yellow-500/10 border border-yellow-200 dark:border-yellow-500/20 rounded-lg p-4">
                <p className="text-yellow-700 dark:text-yellow-400 text-sm">
                  ⚠️ Copy this token now. It will not be shown again.
                </p>
              </div>

              {/* Command Type Tabs */}
              <div className="flex gap-2">
                <button
                  onClick={() => setCommandType('cli')}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-colors ${
                    commandType === 'cli'
                      ? 'bg-orbit-600 text-white'
                      : 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-300 dark:hover:bg-slate-600'
                  }`}
                >
                  <Terminal className="w-4 h-4" />
                  CLI Command
                </button>
                <button
                  onClick={() => setCommandType('docker')}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-colors ${
                    commandType === 'docker'
                      ? 'bg-orbit-600 text-white'
                      : 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-300 dark:hover:bg-slate-600'
                  }`}
                >
                  <Container className="w-4 h-4" />
                  Docker Command
                </button>
              </div>

              {/* Command Display */}
              <div className="relative">
                <pre className="bg-slate-100 dark:bg-slate-900 rounded-lg p-4 text-sm text-slate-700 dark:text-slate-300 overflow-x-auto">
                  {commandType === 'cli' ? cliCommand : dockerCommand}
                </pre>
                <button
                  onClick={() => handleCopy(commandType)}
                  className="absolute top-2 right-2 p-2 bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 rounded-lg transition-colors"
                >
                  {copied === commandType ? (
                    <Check className="w-4 h-4 text-green-600 dark:text-green-400" />
                  ) : (
                    <Copy className="w-4 h-4 text-slate-500 dark:text-slate-400" />
                  )}
                </button>
              </div>

              {/* Instructions */}
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

        <div className="p-6 border-t border-slate-200 dark:border-slate-700 flex justify-end gap-3">
          <button onClick={handleClose} className="btn-secondary">
            {generatedToken ? 'Done' : 'Cancel'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function Agents() {
  const [showAddModal, setShowAddModal] = useState(false)
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
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ['agents'] })}
            className="btn-secondary flex items-center gap-2"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button
            onClick={() => setShowAddModal(true)}
            className="btn-primary flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            Add Agent
          </button>
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
            <button
              onClick={() => setShowAddModal(true)}
              className="btn-primary"
            >
              Add Agent
            </button>
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
                </tr>
              </thead>
              <tbody>
                {agents.map((agent) => (
                  <tr key={agent.id} className="table-row">
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
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Add Agent Modal */}
      <AddAgentModal
        isOpen={showAddModal}
        onClose={() => setShowAddModal(false)}
        serverUrl={serverUrl}
      />
    </div>
  )
}
