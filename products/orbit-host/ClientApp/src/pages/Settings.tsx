import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Key,
  Plus,
  Trash2,
  Copy,
  Check,
  RefreshCw,
  Server,
  Shield,
  Ticket,
} from 'lucide-react'
import { getApiTokens, createApiToken, revokeApiToken, getBootstrapToken, regenerateBootstrapToken, setBootstrapTokenEnabled, setBootstrapTokenAutoApprove } from '@/api/client'
import type { ApiToken, BootstrapToken } from '@/types'
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
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Switch } from '@/components/ui/switch'
import { toast } from '@/components/ui/sonner'

function TokenRow({ token, onRevoke }: { token: ApiToken; onRevoke: () => void }) {
  return (
    <tr className="table-row hover:bg-slate-50 dark:hover:bg-slate-800/50">
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
        <Button
          variant="ghost"
          size="sm"
          onClick={onRevoke}
          className="text-red-600 hover:text-red-500 dark:text-red-400"
        >
          <Trash2 className="w-4 h-4" />
        </Button>
      </td>
    </tr>
  )
}

function CreateTokenDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [name, setName] = useState('')
  const [scopes, setScopes] = useState<string[]>(['api:read'])
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
        toast.success('Token created successfully')
      }
    },
    onError: (error) => {
      toast.error('Failed to create token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const availableScopes = [
    { id: 'api:read', label: 'API Read', description: 'Read-only access to API endpoints' },
    { id: 'api:write', label: 'API Write', description: 'Read and write access to API endpoints' },
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
      try {
        await navigator.clipboard.writeText(newToken)
        setCopied(true)
        toast.success('Copied to clipboard')
        setTimeout(() => setCopied(false), 2000)
      } catch {
        toast.error('Failed to copy to clipboard')
      }
    }
  }

  const handleClose = () => {
    setName('')
    setScopes(['api:read'])
    setExpiresInDays('')
    setNewToken(null)
    setCopied(false)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && handleClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Create API Token</DialogTitle>
          <DialogDescription>
            Create tokens for programmatic access to REST API endpoints.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {newToken ? (
            <>
              <Alert className="border-yellow-200 bg-yellow-50 dark:border-yellow-500/20 dark:bg-yellow-500/10">
                <AlertDescription className="text-yellow-700 dark:text-yellow-400">
                  Copy this token now. It will not be shown again.
                </AlertDescription>
              </Alert>
              <div className="relative">
                <Input
                  readOnly
                  value={newToken}
                  className="pr-12 font-mono text-sm"
                />
                <Button
                  variant="ghost"
                  size="icon"
                  className="absolute right-1 top-1/2 -translate-y-1/2"
                  onClick={handleCopy}
                >
                  {copied ? (
                    <Check className="w-4 h-4 text-green-600" />
                  ) : (
                    <Copy className="w-4 h-4" />
                  )}
                </Button>
              </div>
            </>
          ) : (
            <>
              <div className="space-y-2">
                <Label htmlFor="token-name">Token Name *</Label>
                <Input
                  id="token-name"
                  placeholder="my-token"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label>Scopes</Label>
                <div className="space-y-2">
                  {availableScopes.map((scope) => (
                    <label
                      key={scope.id}
                      className="flex items-start gap-3 p-3 bg-slate-50 dark:bg-slate-900 rounded-lg cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-700/50 border border-transparent hover:border-slate-200 dark:hover:border-slate-700"
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

              <div className="space-y-2">
                <Label htmlFor="expires">Expires In (days, optional)</Label>
                <Input
                  id="expires"
                  type="number"
                  className="w-32"
                  placeholder="Never"
                  value={expiresInDays}
                  onChange={(e) => setExpiresInDays(e.target.value)}
                />
              </div>
            </>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            {newToken ? 'Done' : 'Cancel'}
          </Button>
          {!newToken && (
            <Button
              onClick={handleCreate}
              disabled={!name.trim() || scopes.length === 0 || createMutation.isPending}
            >
              {createMutation.isPending ? 'Creating...' : 'Create Token'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// Bootstrap Token Management - Single Token
function BootstrapTokenCard({ token, onRegenerate }: { token: BootstrapToken; onRegenerate: () => void }) {
  const [copied, setCopied] = useState(false)
  const queryClient = useQueryClient()

  const enabledMutation = useMutation({
    mutationFn: setBootstrapTokenEnabled,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bootstrapToken'] })
      toast.success('Token status updated')
    },
    onError: (error) => {
      toast.error('Failed to update token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const autoApproveMutation = useMutation({
    mutationFn: setBootstrapTokenAutoApprove,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bootstrapToken'] })
      toast.success('Auto-approve setting updated')
    },
    onError: (error) => {
      toast.error('Failed to update setting', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const handleCopy = async () => {
    if (token.token) {
      try {
        await navigator.clipboard.writeText(token.token)
        setCopied(true)
        toast.success('Copied to clipboard')
        setTimeout(() => setCopied(false), 2000)
      } catch {
        toast.error('Failed to copy to clipboard')
      }
    }
  }

  const serverUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000'

  return (
    <div className="space-y-4">
      {/* Workflow explanation */}
      {!token.token && (
        <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg border border-blue-200 dark:border-blue-800">
          <p className="text-sm text-blue-700 dark:text-blue-300">
            <strong>How it works:</strong> Agents use this token for initial enrollment.
            Once enrolled, they receive a certificate for secure, permanent authentication.
            Click "Regenerate Token" to get a new token value (invalidates the old one).
          </p>
        </div>
      )}

      {/* Token Value (only shown after regenerate) */}
      {token.token && (
        <Alert className="border-yellow-200 bg-yellow-50 dark:border-yellow-500/20 dark:bg-yellow-500/10">
          <AlertDescription className="text-yellow-700 dark:text-yellow-400">
            <p className="mb-2 font-medium">New token generated! Copy it now - it will not be shown again.</p>
            <div className="relative">
              <Input
                readOnly
                value={token.token}
                className="pr-12 font-mono text-sm bg-white dark:bg-slate-800"
              />
              <Button
                variant="ghost"
                size="icon"
                className="absolute right-1 top-1/2 -translate-y-1/2"
                onClick={handleCopy}
              >
                {copied ? (
                  <Check className="w-4 h-4 text-green-600" />
                ) : (
                  <Copy className="w-4 h-4" />
                )}
              </Button>
            </div>
            <div className="mt-4 p-3 bg-white dark:bg-slate-800 rounded-lg">
              <p className="text-sm font-medium text-slate-900 dark:text-white mb-2">Agent connection command:</p>
              <pre className="text-xs bg-slate-100 dark:bg-slate-900 p-3 rounded overflow-x-auto whitespace-pre-wrap break-all">
{`// C# Agent
var agent = await MeshAgentBuilder
    .Create("${serverUrl}/agent")
    .WithBootstrapToken("${token.token}")
    .WithName("my-agent")
    .BuildAndConnectAsync();`}
              </pre>
            </div>
          </AlertDescription>
        </Alert>
      )}

      {/* Token Settings */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Enable/Disable Toggle */}
        <div className="flex items-center justify-between p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
          <div>
            <Label className="text-sm font-medium">Token Enabled</Label>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
              Allow agents to enroll using this token
            </p>
          </div>
          <Switch
            checked={token.isEnabled}
            onCheckedChange={(checked) => enabledMutation.mutate(checked)}
            disabled={enabledMutation.isPending}
          />
        </div>

        {/* Auto-Approve Toggle */}
        <div className="flex items-center justify-between p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
          <div>
            <Label className="text-sm font-medium">Auto-Approve</Label>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
              Skip manual approval for enrollments
            </p>
          </div>
          <Switch
            checked={token.autoApprove}
            onCheckedChange={(checked) => autoApproveMutation.mutate(checked)}
            disabled={autoApproveMutation.isPending}
          />
        </div>
      </div>

      {/* Token Info */}
      <div className="grid grid-cols-2 gap-4 text-sm">
        <div className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg">
          <p className="text-slate-500 dark:text-slate-400">Token ID</p>
          <p className="text-slate-900 dark:text-white font-mono mt-1">{token.id.slice(0, 12)}...</p>
        </div>
        <div className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg">
          <p className="text-slate-500 dark:text-slate-400">Last Regenerated</p>
          <p className="text-slate-900 dark:text-white mt-1">
            {token.lastRegeneratedAt
              ? new Date(token.lastRegeneratedAt).toLocaleString()
              : 'Never'}
          </p>
        </div>
      </div>

      {/* Regenerate Button */}
      <div className="flex justify-end">
        <Button onClick={onRegenerate} variant="outline">
          <RefreshCw className="w-4 h-4 mr-2" />
          Regenerate Token
        </Button>
      </div>
    </div>
  )
}

export default function Settings() {
  const [showCreateDialog, setShowCreateDialog] = useState(false)
  const queryClient = useQueryClient()

  const { data: tokens = [], isLoading } = useQuery({
    queryKey: ['tokens'],
    queryFn: getApiTokens,
  })

  const { data: bootstrapToken, isLoading: isLoadingBootstrap } = useQuery({
    queryKey: ['bootstrapToken'],
    queryFn: getBootstrapToken,
  })

  const revokeMutation = useMutation({
    mutationFn: revokeApiToken,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tokens'] })
      toast.success('Token revoked')
    },
    onError: (error) => {
      toast.error('Failed to revoke token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
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
            <div>
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">API Tokens</h2>
              <p className="text-sm text-slate-500 dark:text-slate-400">For programmatic access to REST API endpoints</p>
            </div>
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => queryClient.invalidateQueries({ queryKey: ['tokens'] })}
            >
              <RefreshCw className="w-4 h-4" />
            </Button>
            <Button size="sm" onClick={() => setShowCreateDialog(true)}>
              <Plus className="w-4 h-4 mr-2" />
              Create Token
            </Button>
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
            <Button onClick={() => setShowCreateDialog(true)}>
              Create Token
            </Button>
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

      {/* Bootstrap Token (TOFU Enrollment) */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <Ticket className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <div>
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Bootstrap Token</h2>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Recommended: Trust-On-First-Use enrollment with certificate-based security
              </p>
            </div>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => queryClient.invalidateQueries({ queryKey: ['bootstrapToken'] })}
          >
            <RefreshCw className="w-4 h-4" />
          </Button>
        </div>

        {isLoadingBootstrap ? (
          <div className="text-center py-8">
            <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
            <p className="text-slate-500 dark:text-slate-400">Loading token...</p>
          </div>
        ) : bootstrapToken ? (
          <BootstrapTokenCard
            token={bootstrapToken}
            onRegenerate={() => regenerateMutation.mutate()}
          />
        ) : (
          <div className="text-center py-12">
            <Ticket className="w-12 h-12 text-slate-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">No bootstrap token</h3>
            <p className="text-slate-500 dark:text-slate-400 mb-4">
              Generate a bootstrap token to allow new agents to enroll
            </p>
            <Button onClick={() => regenerateMutation.mutate()}>
              Generate Token
            </Button>
          </div>
        )}
      </div>

      {/* Create Token Dialog */}
      <CreateTokenDialog
        open={showCreateDialog}
        onOpenChange={setShowCreateDialog}
      />
    </div>
  )
}
