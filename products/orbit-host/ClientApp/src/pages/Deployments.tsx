import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Rocket,
  RefreshCw,
  Plus,
  Play,
  Trash2,
  MoreVertical,
  Pencil,
  Eye,
  FolderSync,
  Terminal,
  Clock,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Loader2,
  Server,
} from 'lucide-react'
import {
  getDeploymentProfiles,
  getDeploymentExecutions,
  getDeploymentStatusCounts,
  createDeploymentProfile,
  updateDeploymentProfile,
  deleteDeploymentProfile,
  triggerDeployment,
} from '@/api/client'
import type { DeploymentProfile, DeploymentScript, DeploymentExecution, DeploymentStatus, FileTransferMode } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Switch } from '@/components/ui/switch'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'
import { ChevronDown } from 'lucide-react'
import { toast } from '@/components/ui/sonner'

function DeploymentStatusBadge({ status }: { status: DeploymentStatus }) {
  const config = {
    Pending: { color: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200', icon: Clock },
    InProgress: { color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200', icon: Loader2 },
    Succeeded: { color: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200', icon: CheckCircle2 },
    Failed: { color: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200', icon: XCircle },
    PartialSuccess: { color: 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200', icon: AlertTriangle },
    Cancelled: { color: 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200', icon: XCircle },
  }[status]
  const Icon = config.icon
  return (
    <Badge className={`${config.color} gap-1`}>
      <Icon className={`h-3 w-3 ${status === 'InProgress' ? 'animate-spin' : ''}`} />
      {status}
    </Badge>
  )
}

function ProfileDialog({
  open,
  onOpenChange,
  profile,
  onSave,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  profile?: DeploymentProfile
  onSave: (data: Partial<DeploymentProfile>) => void
}) {
  const [name, setName] = useState(profile?.name || '')
  const [description, setDescription] = useState(profile?.description || '')
  const [sourcePath, setSourcePath] = useState(profile?.sourcePath || '')
  const [targetAgentPattern, setTargetAgentPattern] = useState(profile?.targetAgentPattern || '*')
  const [targetPath, setTargetPath] = useState(profile?.targetPath || '')
  const [watchForChanges, setWatchForChanges] = useState(profile?.watchForChanges ?? true)
  const [debounceMs, setDebounceMs] = useState(profile?.debounceMs?.toString() || '500')
  const [deleteOrphans, setDeleteOrphans] = useState(profile?.deleteOrphans ?? false)
  const [transferMode, setTransferMode] = useState<FileTransferMode>(profile?.transferMode || 'Auto')
  const [isEnabled, setIsEnabled] = useState(profile?.isEnabled ?? true)

  // Scripts
  const [preCommand, setPreCommand] = useState(profile?.preDeployScript?.command || '')
  const [preArgs, setPreArgs] = useState(profile?.preDeployScript?.arguments?.join(' ') || '')
  const [preTimeout, setPreTimeout] = useState(profile?.preDeployScript?.timeoutSeconds?.toString() || '60')
  const [preContinueOnError, setPreContinueOnError] = useState(profile?.preDeployScript?.continueOnError ?? false)

  const [postCommand, setPostCommand] = useState(profile?.postDeployScript?.command || '')
  const [postArgs, setPostArgs] = useState(profile?.postDeployScript?.arguments?.join(' ') || '')
  const [postTimeout, setPostTimeout] = useState(profile?.postDeployScript?.timeoutSeconds?.toString() || '60')
  const [postContinueOnError, setPostContinueOnError] = useState(profile?.postDeployScript?.continueOnError ?? false)

  const handleSave = () => {
    if (!name.trim() || !sourcePath.trim() || !targetAgentPattern.trim() || !targetPath.trim()) {
      toast.error('Please fill in all required fields')
      return
    }

    const preDeployScript: DeploymentScript | undefined = preCommand.trim()
      ? {
          command: preCommand,
          arguments: preArgs.trim() ? preArgs.split(/\s+/) : undefined,
          timeoutSeconds: parseInt(preTimeout) || 60,
          continueOnError: preContinueOnError,
        }
      : undefined

    const postDeployScript: DeploymentScript | undefined = postCommand.trim()
      ? {
          command: postCommand,
          arguments: postArgs.trim() ? postArgs.split(/\s+/) : undefined,
          timeoutSeconds: parseInt(postTimeout) || 60,
          continueOnError: postContinueOnError,
        }
      : undefined

    onSave({
      name,
      description: description || undefined,
      sourcePath,
      targetAgentPattern,
      targetPath,
      watchForChanges,
      debounceMs: parseInt(debounceMs) || 500,
      deleteOrphans,
      transferMode,
      isEnabled,
      preDeployScript,
      postDeployScript,
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{profile ? 'Edit Profile' : 'Create Deployment Profile'}</DialogTitle>
          <DialogDescription>
            Configure a deployment profile to automatically sync files to agents.
          </DialogDescription>
        </DialogHeader>

        <Tabs defaultValue="general" className="w-full">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="general">General</TabsTrigger>
            <TabsTrigger value="sync">Sync Settings</TabsTrigger>
            <TabsTrigger value="scripts">Scripts</TabsTrigger>
          </TabsList>

          <TabsContent value="general" className="space-y-4 mt-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name *</Label>
              <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="My Deployment" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Description of this deployment profile" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sourcePath">Source Path *</Label>
              <Input id="sourcePath" value={sourcePath} onChange={(e) => setSourcePath(e.target.value)} placeholder="C:\Deploy\MyApp" />
              <p className="text-xs text-muted-foreground">Local folder on the server containing files to deploy</p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="targetAgentPattern">Target Agent Pattern *</Label>
              <Input id="targetAgentPattern" value={targetAgentPattern} onChange={(e) => setTargetAgentPattern(e.target.value)} placeholder="customer-*" />
              <p className="text-xs text-muted-foreground">Wildcards supported: * matches any characters, ? matches single character</p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="targetPath">Target Path *</Label>
              <Input id="targetPath" value={targetPath} onChange={(e) => setTargetPath(e.target.value)} placeholder="C:\inetpub\wwwroot\MyApp" />
              <p className="text-xs text-muted-foreground">Destination folder on target agents</p>
            </div>
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Enabled</Label>
                <p className="text-xs text-muted-foreground">Profile is active and can be deployed</p>
              </div>
              <Switch checked={isEnabled} onCheckedChange={setIsEnabled} />
            </div>
          </TabsContent>

          <TabsContent value="sync" className="space-y-4 mt-4">
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Watch for Changes</Label>
                <p className="text-xs text-muted-foreground">Automatically deploy when source files change</p>
              </div>
              <Switch checked={watchForChanges} onCheckedChange={setWatchForChanges} />
            </div>
            {watchForChanges && (
              <div className="space-y-2">
                <Label htmlFor="debounceMs">Debounce (ms)</Label>
                <Input id="debounceMs" type="number" value={debounceMs} onChange={(e) => setDebounceMs(e.target.value)} />
                <p className="text-xs text-muted-foreground">Wait time after last file change before triggering deployment</p>
              </div>
            )}
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Delete Orphans</Label>
                <p className="text-xs text-muted-foreground">Remove files on target that don't exist in source</p>
              </div>
              <Switch checked={deleteOrphans} onCheckedChange={setDeleteOrphans} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="transferMode">Transfer Mode</Label>
              <Select value={transferMode} onValueChange={(v) => setTransferMode(v as FileTransferMode)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Auto">Auto (recommended)</SelectItem>
                  <SelectItem value="Http">HTTP Download</SelectItem>
                  <SelectItem value="P2P">P2P Direct</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </TabsContent>

          <TabsContent value="scripts" className="space-y-4 mt-4">
            <Collapsible className="border rounded-lg">
              <CollapsibleTrigger className="flex items-center justify-between w-full p-4 hover:bg-muted/50">
                <div className="flex items-center gap-2">
                  <Terminal className="h-4 w-4" />
                  <span className="font-medium">Pre-Deploy Script</span>
                </div>
                <ChevronDown className="h-4 w-4" />
              </CollapsibleTrigger>
              <CollapsibleContent className="p-4 pt-0 space-y-4">
                <div className="space-y-2">
                  <Label>Command</Label>
                  <Input value={preCommand} onChange={(e) => setPreCommand(e.target.value)} placeholder="powershell.exe" />
                </div>
                <div className="space-y-2">
                  <Label>Arguments</Label>
                  <Input value={preArgs} onChange={(e) => setPreArgs(e.target.value)} placeholder="-Command Stop-IISAppPool -Name 'MyApp'" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Timeout (seconds)</Label>
                    <Input type="number" value={preTimeout} onChange={(e) => setPreTimeout(e.target.value)} />
                  </div>
                  <div className="flex items-center gap-2 pt-6">
                    <Switch checked={preContinueOnError} onCheckedChange={setPreContinueOnError} />
                    <Label>Continue on error</Label>
                  </div>
                </div>
              </CollapsibleContent>
            </Collapsible>

            <Collapsible className="border rounded-lg">
              <CollapsibleTrigger className="flex items-center justify-between w-full p-4 hover:bg-muted/50">
                <div className="flex items-center gap-2">
                  <Terminal className="h-4 w-4" />
                  <span className="font-medium">Post-Deploy Script</span>
                </div>
                <ChevronDown className="h-4 w-4" />
              </CollapsibleTrigger>
              <CollapsibleContent className="p-4 pt-0 space-y-4">
                <div className="space-y-2">
                  <Label>Command</Label>
                  <Input value={postCommand} onChange={(e) => setPostCommand(e.target.value)} placeholder="powershell.exe" />
                </div>
                <div className="space-y-2">
                  <Label>Arguments</Label>
                  <Input value={postArgs} onChange={(e) => setPostArgs(e.target.value)} placeholder="-Command Start-IISAppPool -Name 'MyApp'" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Timeout (seconds)</Label>
                    <Input type="number" value={postTimeout} onChange={(e) => setPostTimeout(e.target.value)} />
                  </div>
                  <div className="flex items-center gap-2 pt-6">
                    <Switch checked={postContinueOnError} onCheckedChange={setPostContinueOnError} />
                    <Label>Continue on error</Label>
                  </div>
                </div>
              </CollapsibleContent>
            </Collapsible>
          </TabsContent>
        </Tabs>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={handleSave}>{profile ? 'Save Changes' : 'Create Profile'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function ExecutionDetailDialog({
  open,
  onOpenChange,
  execution,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  execution: DeploymentExecution | null
}) {
  if (!execution) return null

  const formatDuration = (start: string, end?: string) => {
    if (!end) return 'In progress...'
    const ms = new Date(end).getTime() - new Date(start).getTime()
    const seconds = Math.floor(ms / 1000)
    if (seconds < 60) return `${seconds}s`
    const minutes = Math.floor(seconds / 60)
    return `${minutes}m ${seconds % 60}s`
  }

  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            Deployment Execution
            <DeploymentStatusBadge status={execution.status} />
          </DialogTitle>
          <DialogDescription>
            Execution ID: {execution.id}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="space-y-1">
              <p className="text-sm text-muted-foreground">Trigger</p>
              <p className="font-medium">{execution.trigger}</p>
            </div>
            <div className="space-y-1">
              <p className="text-sm text-muted-foreground">Duration</p>
              <p className="font-medium">{formatDuration(execution.startedAt, execution.completedAt)}</p>
            </div>
            <div className="space-y-1">
              <p className="text-sm text-muted-foreground">Files Transferred</p>
              <p className="font-medium">{execution.filesTransferred}</p>
            </div>
            <div className="space-y-1">
              <p className="text-sm text-muted-foreground">Data Transferred</p>
              <p className="font-medium">{formatBytes(execution.bytesTransferred)}</p>
            </div>
          </div>

          <div className="space-y-1">
            <p className="text-sm text-muted-foreground">Agents</p>
            <p className="font-medium">
              {execution.successfulAgents} succeeded, {execution.failedAgents} failed of {execution.totalAgents} total
            </p>
          </div>

          {execution.errorMessage && (
            <div className="rounded-md bg-red-50 dark:bg-red-950 p-4">
              <p className="text-sm text-red-800 dark:text-red-200">{execution.errorMessage}</p>
            </div>
          )}

          {execution.agentResults && execution.agentResults.length > 0 && (
            <div className="space-y-2">
              <p className="text-sm font-medium">Agent Results</p>
              <div className="space-y-2">
                {execution.agentResults.map((result) => (
                  <div key={result.agentId} className="border rounded-md p-3 space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <Server className="h-4 w-4" />
                        <span className="font-medium">{result.agentName || result.agentId}</span>
                      </div>
                      <Badge variant={result.status === 'Succeeded' ? 'default' : result.status === 'Failed' ? 'destructive' : 'secondary'}>
                        {result.status}
                      </Badge>
                    </div>
                    {result.errorMessage && (
                      <p className="text-sm text-red-600 dark:text-red-400">{result.errorMessage}</p>
                    )}
                    {result.fileSyncResult && (
                      <div className="text-sm text-muted-foreground">
                        Files: +{result.fileSyncResult.filesCreated} updated:{result.fileSyncResult.filesUpdated} -{result.fileSyncResult.filesDeleted} | {formatBytes(result.fileSyncResult.bytesTransferred)}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default function Deployments() {
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingProfile, setEditingProfile] = useState<DeploymentProfile | undefined>()
  const [selectedExecution, setSelectedExecution] = useState<DeploymentExecution | null>(null)
  const [executionDialogOpen, setExecutionDialogOpen] = useState(false)

  const queryClient = useQueryClient()

  const { data: profiles, isLoading: profilesLoading } = useQuery({
    queryKey: ['deployment-profiles'],
    queryFn: getDeploymentProfiles,
  })

  const { data: executions, isLoading: executionsLoading } = useQuery({
    queryKey: ['deployment-executions'],
    queryFn: () => getDeploymentExecutions({ pageSize: 10 }),
  })

  const { data: statusCounts } = useQuery({
    queryKey: ['deployment-status-counts'],
    queryFn: getDeploymentStatusCounts,
    refetchInterval: 5000,
  })

  const createMutation = useMutation({
    mutationFn: createDeploymentProfile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployment-profiles'] })
      toast.success('Profile created successfully')
      setDialogOpen(false)
    },
    onError: (error) => {
      toast.error('Failed to create profile', { description: error instanceof Error ? error.message : 'Unknown error' })
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<DeploymentProfile> }) => updateDeploymentProfile(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployment-profiles'] })
      toast.success('Profile updated successfully')
      setDialogOpen(false)
      setEditingProfile(undefined)
    },
    onError: (error) => {
      toast.error('Failed to update profile', { description: error instanceof Error ? error.message : 'Unknown error' })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteDeploymentProfile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployment-profiles'] })
      toast.success('Profile deleted successfully')
    },
    onError: (error) => {
      toast.error('Failed to delete profile', { description: error instanceof Error ? error.message : 'Unknown error' })
    },
  })

  const deployMutation = useMutation({
    mutationFn: triggerDeployment,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployment-executions'] })
      queryClient.invalidateQueries({ queryKey: ['deployment-status-counts'] })
      toast.success('Deployment triggered successfully')
    },
    onError: (error) => {
      toast.error('Failed to trigger deployment', { description: error instanceof Error ? error.message : 'Unknown error' })
    },
  })

  const handleSaveProfile = (data: Partial<DeploymentProfile>) => {
    if (editingProfile) {
      updateMutation.mutate({ id: editingProfile.id, data })
    } else {
      createMutation.mutate(data as Parameters<typeof createDeploymentProfile>[0])
    }
  }

  const handleEdit = (profile: DeploymentProfile) => {
    setEditingProfile(profile)
    setDialogOpen(true)
  }

  const handleDelete = (profile: DeploymentProfile) => {
    if (confirm(`Delete profile "${profile.name}"?`)) {
      deleteMutation.mutate(profile.id)
    }
  }

  const handleDeploy = (profile: DeploymentProfile) => {
    deployMutation.mutate(profile.id)
  }

  const handleViewExecution = (execution: DeploymentExecution) => {
    setSelectedExecution(execution)
    setExecutionDialogOpen(true)
  }

  const formatDate = (date: string) => {
    return new Date(date).toLocaleString()
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Rocket className="h-6 w-6" />
            Deployments
          </h1>
          <p className="text-muted-foreground">Manage deployment profiles and monitor deployments</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="icon" onClick={() => queryClient.invalidateQueries()}>
            <RefreshCw className="h-4 w-4" />
          </Button>
          <Button onClick={() => { setEditingProfile(undefined); setDialogOpen(true) }}>
            <Plus className="h-4 w-4 mr-2" />
            New Profile
          </Button>
        </div>
      </div>

      {/* Status Overview */}
      {statusCounts && (
        <div className="grid grid-cols-2 md:grid-cols-6 gap-4">
          <div className="border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Pending</p>
            <p className="text-2xl font-bold">{statusCounts.pending}</p>
          </div>
          <div className="border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">In Progress</p>
            <p className="text-2xl font-bold text-blue-600">{statusCounts.inProgress}</p>
          </div>
          <div className="border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Succeeded</p>
            <p className="text-2xl font-bold text-green-600">{statusCounts.succeeded}</p>
          </div>
          <div className="border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Failed</p>
            <p className="text-2xl font-bold text-red-600">{statusCounts.failed}</p>
          </div>
          <div className="border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Partial</p>
            <p className="text-2xl font-bold text-orange-600">{statusCounts.partialSuccess}</p>
          </div>
          <div className="border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Cancelled</p>
            <p className="text-2xl font-bold text-gray-600">{statusCounts.cancelled}</p>
          </div>
        </div>
      )}

      <Tabs defaultValue="profiles" className="w-full">
        <TabsList>
          <TabsTrigger value="profiles">Profiles</TabsTrigger>
          <TabsTrigger value="executions">Execution History</TabsTrigger>
        </TabsList>

        <TabsContent value="profiles" className="mt-4">
          {profilesLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : !profiles?.length ? (
            <div className="text-center py-12 border rounded-lg">
              <FolderSync className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
              <p className="text-lg font-medium">No Deployment Profiles</p>
              <p className="text-muted-foreground mb-4">Create a profile to start deploying files to agents</p>
              <Button onClick={() => setDialogOpen(true)}>
                <Plus className="h-4 w-4 mr-2" />
                Create Profile
              </Button>
            </div>
          ) : (
            <div className="grid gap-4">
              {profiles.map((profile) => (
                <div key={profile.id} className="border rounded-lg p-4 space-y-3">
                  <div className="flex items-start justify-between">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <h3 className="font-semibold">{profile.name}</h3>
                        {!profile.isEnabled && <Badge variant="secondary">Disabled</Badge>}
                        {profile.watchForChanges && <Badge variant="outline">Auto-watch</Badge>}
                      </div>
                      {profile.description && <p className="text-sm text-muted-foreground">{profile.description}</p>}
                    </div>
                    <div className="flex items-center gap-2">
                      <Button size="sm" onClick={() => handleDeploy(profile)} disabled={!profile.isEnabled || deployMutation.isPending}>
                        {deployMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />}
                        <span className="ml-2">Deploy</span>
                      </Button>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon">
                            <MoreVertical className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => handleEdit(profile)}>
                            <Pencil className="h-4 w-4 mr-2" />
                            Edit
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem className="text-red-600" onClick={() => handleDelete(profile)}>
                            <Trash2 className="h-4 w-4 mr-2" />
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                    <div>
                      <p className="text-muted-foreground">Source</p>
                      <p className="font-mono text-xs truncate">{profile.sourcePath}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Target Pattern</p>
                      <p className="font-mono text-xs">{profile.targetAgentPattern}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Target Path</p>
                      <p className="font-mono text-xs truncate">{profile.targetPath}</p>
                    </div>
                  </div>
                  {profile.lastDeployedAt && (
                    <p className="text-xs text-muted-foreground">Last deployed: {formatDate(profile.lastDeployedAt)}</p>
                  )}
                </div>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="executions" className="mt-4">
          {executionsLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : !executions?.items?.length ? (
            <div className="text-center py-12 border rounded-lg">
              <Clock className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
              <p className="text-lg font-medium">No Execution History</p>
              <p className="text-muted-foreground">Deployment executions will appear here</p>
            </div>
          ) : (
            <div className="space-y-2">
              {executions.items.map((execution) => (
                <div key={execution.id} className="border rounded-lg p-4 flex items-center justify-between hover:bg-muted/50 cursor-pointer" onClick={() => handleViewExecution(execution)}>
                  <div className="flex items-center gap-4">
                    <DeploymentStatusBadge status={execution.status} />
                    <div>
                      <p className="font-medium">Profile: {execution.profileId}</p>
                      <p className="text-xs text-muted-foreground">
                        {execution.trigger} | {formatDate(execution.startedAt)} | {execution.successfulAgents}/{execution.totalAgents} agents
                      </p>
                    </div>
                  </div>
                  <Button variant="ghost" size="icon">
                    <Eye className="h-4 w-4" />
                  </Button>
                </div>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <ProfileDialog
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open)
          if (!open) setEditingProfile(undefined)
        }}
        profile={editingProfile}
        onSave={handleSaveProfile}
      />

      <ExecutionDetailDialog
        open={executionDialogOpen}
        onOpenChange={setExecutionDialogOpen}
        execution={selectedExecution}
      />
    </div>
  )
}
