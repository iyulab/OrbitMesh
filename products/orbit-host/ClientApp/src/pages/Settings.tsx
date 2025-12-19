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
  Clock,
  CheckCircle2,
} from 'lucide-react'
import { getApiTokens, createApiToken, revokeApiToken, getBootstrapTokens, createBootstrapToken, revokeBootstrapToken } from '@/api/client'
import type { ApiToken, BootstrapToken, CreateBootstrapTokenRequest } from '@/types'
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
    setScopes(['agent:connect'])
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
            Create a new token for API or agent access
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

// Bootstrap Token Components
function BootstrapTokenRow({ token, onRevoke }: { token: BootstrapToken; onRevoke: () => void }) {
  const isExpired = new Date(token.expiresAt) < new Date()
  const timeLeft = getTimeLeft(token.expiresAt)

  return (
    <tr className="table-row hover:bg-slate-50 dark:hover:bg-slate-800/50">
      <td className="py-3 px-4">
        <div className="flex items-center gap-2">
          <Ticket className="w-4 h-4 text-slate-400" />
          <span className="text-slate-900 dark:text-white font-medium">
            {token.description || token.id.slice(0, 8)}
          </span>
          {token.autoApprove && (
            <span className="px-1.5 py-0.5 bg-green-100 dark:bg-green-500/20 text-green-700 dark:text-green-400 rounded text-xs">
              Auto-approve
            </span>
          )}
        </div>
      </td>
      <td className="py-3 px-4">
        {token.isConsumed ? (
          <span className="flex items-center gap-1 text-sm text-green-600 dark:text-green-400">
            <CheckCircle2 className="w-4 h-4" />
            Used
          </span>
        ) : isExpired ? (
          <span className="text-sm text-red-500">Expired</span>
        ) : (
          <span className="flex items-center gap-1 text-sm text-slate-500 dark:text-slate-400">
            <Clock className="w-4 h-4" />
            {timeLeft}
          </span>
        )}
      </td>
      <td className="py-3 px-4">
        <span className="text-sm text-slate-500 dark:text-slate-400">
          {new Date(token.createdAt).toLocaleDateString()}
        </span>
      </td>
      <td className="py-3 px-4">
        <span className="text-sm text-slate-500 dark:text-slate-400">
          {token.consumedByNodeId || '-'}
        </span>
      </td>
      <td className="py-3 px-4 text-right">
        {!token.isConsumed && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onRevoke}
            className="text-red-600 hover:text-red-500 dark:text-red-400"
          >
            <Trash2 className="w-4 h-4" />
          </Button>
        )}
      </td>
    </tr>
  )
}

function getTimeLeft(expiresAt: string): string {
  const now = new Date()
  const expires = new Date(expiresAt)
  const diff = expires.getTime() - now.getTime()

  if (diff <= 0) return 'Expired'

  const hours = Math.floor(diff / (1000 * 60 * 60))
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60))

  if (hours > 24) {
    const days = Math.floor(hours / 24)
    return `${days}d ${hours % 24}h`
  }
  return `${hours}h ${minutes}m`
}

function CreateBootstrapTokenDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [description, setDescription] = useState('')
  const [expirationHours, setExpirationHours] = useState('24')
  const [autoApprove, setAutoApprove] = useState(false)
  const [newToken, setNewToken] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const queryClient = useQueryClient()

  const createMutation = useMutation({
    mutationFn: createBootstrapToken,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['bootstrapTokens'] })
      if (data.token) {
        setNewToken(data.token)
        toast.success('Bootstrap token created')
      }
    },
    onError: (error) => {
      toast.error('Failed to create token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const handleCreate = () => {
    const request: CreateBootstrapTokenRequest = {
      description: description || undefined,
      expirationHours: parseInt(expirationHours) || 24,
      autoApprove,
    }
    createMutation.mutate(request)
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
    setDescription('')
    setExpirationHours('24')
    setAutoApprove(false)
    setNewToken(null)
    setCopied(false)
    onOpenChange(false)
  }

  const serverUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000'

  return (
    <Dialog open={open} onOpenChange={(o) => !o && handleClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create Bootstrap Token</DialogTitle>
          <DialogDescription>
            Create a one-time token for agent enrollment (TOFU)
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

              {/* Usage instructions */}
              <div className="mt-4 p-4 bg-slate-50 dark:bg-slate-900 rounded-lg space-y-3">
                <p className="text-sm font-medium text-slate-900 dark:text-white">Agent connection command:</p>
                <pre className="text-xs bg-slate-100 dark:bg-slate-800 p-3 rounded overflow-x-auto">
{`// C# Agent
var agent = await MeshAgentBuilder
    .Create("${serverUrl}/agent")
    .WithBootstrapToken("${newToken}")
    .WithName("my-agent")
    .BuildAndConnectAsync();`}
                </pre>
              </div>
            </>
          ) : (
            <>
              <div className="space-y-2">
                <Label htmlFor="token-desc">Description (optional)</Label>
                <Input
                  id="token-desc"
                  placeholder="e.g., Production server agent"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="expires-hours">Expires In (hours)</Label>
                <Input
                  id="expires-hours"
                  type="number"
                  className="w-32"
                  value={expirationHours}
                  onChange={(e) => setExpirationHours(e.target.value)}
                />
                <p className="text-xs text-slate-500">Token expires after this many hours</p>
              </div>

              <div className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900 rounded-lg">
                <div>
                  <Label htmlFor="auto-approve" className="text-sm font-medium">Auto-approve enrollment</Label>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    Skip manual approval when agent enrolls with this token
                  </p>
                </div>
                <Switch
                  id="auto-approve"
                  checked={autoApprove}
                  onCheckedChange={setAutoApprove}
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
              disabled={createMutation.isPending}
            >
              {createMutation.isPending ? 'Creating...' : 'Create Token'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default function Settings() {
  const [showCreateDialog, setShowCreateDialog] = useState(false)
  const [showBootstrapDialog, setShowBootstrapDialog] = useState(false)
  const queryClient = useQueryClient()

  const { data: tokens = [], isLoading } = useQuery({
    queryKey: ['tokens'],
    queryFn: getApiTokens,
  })

  const { data: bootstrapTokens = [], isLoading: isLoadingBootstrap } = useQuery({
    queryKey: ['bootstrapTokens'],
    queryFn: getBootstrapTokens,
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

  const revokeBootstrapMutation = useMutation({
    mutationFn: revokeBootstrapToken,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bootstrapTokens'] })
      toast.success('Bootstrap token revoked')
    },
    onError: (error) => {
      toast.error('Failed to revoke token', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  // Filter active bootstrap tokens (not consumed, not expired)
  const activeBootstrapTokens = bootstrapTokens.filter(
    (t) => !t.isConsumed && new Date(t.expiresAt) > new Date()
  )

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

      {/* Bootstrap Tokens (TOFU Enrollment) */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <Ticket className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <div>
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Bootstrap Tokens</h2>
              <p className="text-sm text-slate-500 dark:text-slate-400">One-time tokens for agent enrollment (TOFU)</p>
            </div>
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => queryClient.invalidateQueries({ queryKey: ['bootstrapTokens'] })}
            >
              <RefreshCw className="w-4 h-4" />
            </Button>
            <Button size="sm" onClick={() => setShowBootstrapDialog(true)}>
              <Plus className="w-4 h-4 mr-2" />
              Create Token
            </Button>
          </div>
        </div>

        {isLoadingBootstrap ? (
          <div className="text-center py-8">
            <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
            <p className="text-slate-500 dark:text-slate-400">Loading tokens...</p>
          </div>
        ) : bootstrapTokens.length === 0 ? (
          <div className="text-center py-12">
            <Ticket className="w-12 h-12 text-slate-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">No bootstrap tokens</h3>
            <p className="text-slate-500 dark:text-slate-400 mb-4">
              Create bootstrap tokens to allow new agents to enroll securely
            </p>
            <Button onClick={() => setShowBootstrapDialog(true)}>
              Create Bootstrap Token
            </Button>
          </div>
        ) : (
          <>
            {activeBootstrapTokens.length > 0 && (
              <Alert className="mb-4 border-blue-200 bg-blue-50 dark:border-blue-500/20 dark:bg-blue-500/10">
                <AlertDescription className="text-blue-700 dark:text-blue-400">
                  {activeBootstrapTokens.length} active token{activeBootstrapTokens.length > 1 ? 's' : ''} available for agent enrollment
                </AlertDescription>
              </Alert>
            )}
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="table-header">
                    <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Description</th>
                    <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Status</th>
                    <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Created</th>
                    <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Used By</th>
                    <th className="text-right py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {bootstrapTokens.map((token) => (
                    <BootstrapTokenRow
                      key={token.id}
                      token={token}
                      onRevoke={() => revokeBootstrapMutation.mutate(token.id)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>

      {/* Create Token Dialog */}
      <CreateTokenDialog
        open={showCreateDialog}
        onOpenChange={setShowCreateDialog}
      />

      {/* Create Bootstrap Token Dialog */}
      <CreateBootstrapTokenDialog
        open={showBootstrapDialog}
        onOpenChange={setShowBootstrapDialog}
      />
    </div>
  )
}
