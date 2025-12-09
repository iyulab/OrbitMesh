import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Settings as SettingsIcon,
  Key,
  Plus,
  Trash2,
  Copy,
  Check,
  RefreshCw,
  Server,
  Shield,
} from 'lucide-react'
import { getApiTokens, createApiToken, revokeApiToken } from '@/api/client'
import type { ApiToken } from '@/types'

function TokenRow({ token, onRevoke }: { token: ApiToken; onRevoke: () => void }) {
  return (
    <tr className="table-row">
      <td className="py-3 px-4">
        <div className="flex items-center gap-2">
          <Key className="w-4 h-4 text-slate-400" />
          <span className="text-slate-900 dark:text-white font-medium">{token.name}</span>
        </div>
      </td>
      <td className="py-3 px-4">
        <div className="flex flex-wrap gap-1">
          {token.scopes.map((scope) => (
            <span
              key={scope}
              className="px-2 py-0.5 bg-slate-200 dark:bg-slate-700 rounded text-xs text-slate-700 dark:text-slate-300"
            >
              {scope}
            </span>
          ))}
        </div>
      </td>
      <td className="py-3 px-4">
        <span className="text-sm text-slate-500 dark:text-slate-400">
          {new Date(token.createdAt).toLocaleDateString()}
        </span>
      </td>
      <td className="py-3 px-4">
        <span className="text-sm text-slate-500 dark:text-slate-400">
          {token.lastUsedAt
            ? new Date(token.lastUsedAt).toLocaleDateString()
            : 'Never'}
        </span>
      </td>
      <td className="py-3 px-4">
        <span className="text-sm text-slate-500 dark:text-slate-400">
          {token.expiresAt
            ? new Date(token.expiresAt).toLocaleDateString()
            : 'Never'}
        </span>
      </td>
      <td className="py-3 px-4 text-right">
        <button
          onClick={onRevoke}
          className="text-red-600 hover:text-red-500 dark:text-red-400 dark:hover:text-red-300 p-1"
          title="Revoke token"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      </td>
    </tr>
  )
}

function CreateTokenModal({
  isOpen,
  onClose,
}: {
  isOpen: boolean
  onClose: () => void
}) {
  const [name, setName] = useState('')
  const [scopes, setScopes] = useState<string[]>(['agent:connect'])
  const [expiresInDays, setExpiresInDays] = useState('')
  const [newToken, setNewToken] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const queryClient = useQueryClient()

  const createMutation = useMutation({
    mutationFn: createApiToken,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['tokens'] })
      if (data.token) {
        setNewToken(data.token)
      }
    },
  })

  const availableScopes = [
    { id: 'agent:connect', label: 'Agent Connect', description: 'Allow agent connections' },
    { id: 'api:read', label: 'API Read', description: 'Read API access' },
    { id: 'api:write', label: 'API Write', description: 'Write API access' },
    { id: 'admin', label: 'Admin', description: 'Full administrative access' },
  ]

  const handleScopeToggle = (scope: string) => {
    setScopes((prev) =>
      prev.includes(scope)
        ? prev.filter((s) => s !== scope)
        : [...prev, scope]
    )
  }

  const handleCreate = () => {
    createMutation.mutate({
      name,
      scopes,
      expiresInDays: expiresInDays ? parseInt(expiresInDays) : undefined,
    })
  }

  const handleCopy = async () => {
    if (newToken) {
      await navigator.clipboard.writeText(newToken)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const handleClose = () => {
    setName('')
    setScopes(['agent:connect'])
    setExpiresInDays('')
    setNewToken(null)
    setCopied(false)
    onClose()
  }

  if (!isOpen) return null

  return (
    <div className="modal-backdrop">
      <div className="modal-content w-full max-w-md">
        <div className="p-6 border-b border-slate-200 dark:border-slate-700">
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Create API Token</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            Create a new token for API or agent access
          </p>
        </div>

        <div className="p-6 space-y-4">
          {newToken ? (
            <>
              <div className="bg-yellow-50 dark:bg-yellow-500/10 border border-yellow-200 dark:border-yellow-500/20 rounded-lg p-4">
                <p className="text-yellow-700 dark:text-yellow-400 text-sm">
                  ⚠️ Copy this token now. It will not be shown again.
                </p>
              </div>
              <div className="relative">
                <input
                  type="text"
                  readOnly
                  value={newToken}
                  className="input w-full pr-12 font-mono text-sm"
                />
                <button
                  onClick={handleCopy}
                  className="absolute right-2 top-1/2 -translate-y-1/2 p-1"
                >
                  {copied ? (
                    <Check className="w-5 h-5 text-green-600 dark:text-green-400" />
                  ) : (
                    <Copy className="w-5 h-5 text-slate-400" />
                  )}
                </button>
              </div>
            </>
          ) : (
            <>
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                  Token Name *
                </label>
                <input
                  type="text"
                  className="input w-full"
                  placeholder="my-token"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                  Scopes
                </label>
                <div className="space-y-2">
                  {availableScopes.map((scope) => (
                    <label
                      key={scope.id}
                      className="flex items-start gap-3 p-3 bg-slate-50 dark:bg-slate-900 rounded-lg cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-700/50"
                    >
                      <input
                        type="checkbox"
                        checked={scopes.includes(scope.id)}
                        onChange={() => handleScopeToggle(scope.id)}
                        className="mt-0.5"
                      />
                      <div>
                        <p className="text-slate-900 dark:text-white text-sm font-medium">{scope.label}</p>
                        <p className="text-xs text-slate-500 dark:text-slate-400">{scope.description}</p>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                  Expires In (days, optional)
                </label>
                <input
                  type="number"
                  className="input w-32"
                  placeholder="Never"
                  value={expiresInDays}
                  onChange={(e) => setExpiresInDays(e.target.value)}
                />
              </div>
            </>
          )}
        </div>

        <div className="p-6 border-t border-slate-200 dark:border-slate-700 flex justify-end gap-3">
          <button onClick={handleClose} className="btn-secondary">
            {newToken ? 'Done' : 'Cancel'}
          </button>
          {!newToken && (
            <button
              onClick={handleCreate}
              disabled={!name.trim() || scopes.length === 0 || createMutation.isPending}
              className="btn-primary"
            >
              {createMutation.isPending ? 'Creating...' : 'Create Token'}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

export default function Settings() {
  const [showCreateModal, setShowCreateModal] = useState(false)
  const queryClient = useQueryClient()

  const { data: tokens = [], isLoading } = useQuery({
    queryKey: ['tokens'],
    queryFn: getApiTokens,
  })

  const revokeMutation = useMutation({
    mutationFn: revokeApiToken,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tokens'] })
    },
  })

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Settings</h1>
        <p className="text-slate-500 dark:text-slate-400 mt-1">Configure your OrbitMesh server</p>
      </div>

      {/* Server Info */}
      <div className="card">
        <div className="flex items-center gap-3 mb-4">
          <Server className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Server Information</h2>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <p className="text-sm text-slate-500 dark:text-slate-400">Server URL</p>
            <p className="text-slate-900 dark:text-white font-mono text-sm mt-1">
              {typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000'}
            </p>
          </div>
          <div className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <p className="text-sm text-slate-500 dark:text-slate-400">Agent Hub Endpoint</p>
            <p className="text-slate-900 dark:text-white font-mono text-sm mt-1">/agent</p>
          </div>
        </div>
      </div>

      {/* API Tokens */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <Shield className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">API Tokens</h2>
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => queryClient.invalidateQueries({ queryKey: ['tokens'] })}
              className="btn-secondary text-sm flex items-center gap-1"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
            <button
              onClick={() => setShowCreateModal(true)}
              className="btn-primary text-sm flex items-center gap-1"
            >
              <Plus className="w-4 h-4" />
              Create Token
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="text-center py-8">
            <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
            <p className="text-slate-500 dark:text-slate-400">Loading tokens...</p>
          </div>
        ) : tokens.length === 0 ? (
          <div className="text-center py-12">
            <Key className="w-12 h-12 text-slate-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">No API tokens</h3>
            <p className="text-slate-500 dark:text-slate-400 mb-4">
              Create tokens to allow agents or applications to connect
            </p>
            <button
              onClick={() => setShowCreateModal(true)}
              className="btn-primary"
            >
              Create Token
            </button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="table-header">
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Name</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Scopes</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Created</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Last Used</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Expires</th>
                  <th className="text-right py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Actions</th>
                </tr>
              </thead>
              <tbody>
                {tokens.map((token) => (
                  <TokenRow
                    key={token.id}
                    token={token}
                    onRevoke={() => revokeMutation.mutate(token.id)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Create Token Modal */}
      <CreateTokenModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
      />
    </div>
  )
}
