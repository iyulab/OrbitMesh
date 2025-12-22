import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  FolderSync,
  Plus,
  Play,
  Loader2,
  Server,
  FolderInput,
  FolderOutput,
} from 'lucide-react'
import {
  getDeploymentProfiles,
  createDeploymentProfile,
  triggerDeployment,
} from '@/api/client'
import type { DeploymentProfile, Agent, FileTransferMode } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { toast } from '@/components/ui/sonner'

interface AgentFileSyncDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  agent: Agent
}

// Check if agent name matches the pattern (simple wildcard matching)
function matchesPattern(name: string, pattern: string): boolean {
  if (pattern === '*') return true

  // Convert wildcard pattern to regex
  const regexPattern = pattern
    .replace(/[.+^${}()|[\]\\]/g, '\\$&') // Escape special chars except * and ?
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.')

  const regex = new RegExp(`^${regexPattern}$`, 'i')
  return regex.test(name)
}

export function AgentFileSyncDialog({
  open,
  onOpenChange,
  agent,
}: AgentFileSyncDialogProps) {
  const [activeTab, setActiveTab] = useState<'profiles' | 'create'>('profiles')
  const [deployingProfile, setDeployingProfile] = useState<string | null>(null)

  // Create form state
  const [name, setName] = useState(`${agent.name}-sync`)
  const [sourcePath, setSourcePath] = useState('')
  const [targetPath, setTargetPath] = useState('')
  const [deleteOrphans, setDeleteOrphans] = useState(false)
  const [transferMode, setTransferMode] = useState<FileTransferMode>('Auto')

  const queryClient = useQueryClient()

  const { data: allProfiles = [], isLoading } = useQuery({
    queryKey: ['deployment-profiles'],
    queryFn: getDeploymentProfiles,
  })

  // Filter profiles that match this agent
  const matchingProfiles = allProfiles.filter(
    (p) => p.isEnabled && matchesPattern(agent.name, p.targetAgentPattern)
  )

  const createMutation = useMutation({
    mutationFn: createDeploymentProfile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployment-profiles'] })
      toast.success('Sync profile created successfully')
      setActiveTab('profiles')
      // Reset form
      setName(`${agent.name}-sync`)
      setSourcePath('')
      setTargetPath('')
      setDeleteOrphans(false)
      setTransferMode('Auto')
    },
    onError: (error) => {
      toast.error('Failed to create profile', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const deployMutation = useMutation({
    mutationFn: triggerDeployment,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployment-executions'] })
      toast.success('Deployment started')
      setDeployingProfile(null)
    },
    onError: (error) => {
      toast.error('Failed to start deployment', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
      setDeployingProfile(null)
    },
  })

  // Reset form when agent changes
  useEffect(() => {
    setName(`${agent.name}-sync`)
  }, [agent.name])

  const handleCreate = () => {
    if (!name.trim() || !sourcePath.trim() || !targetPath.trim()) {
      toast.error('Please fill in all required fields')
      return
    }

    createMutation.mutate({
      name,
      sourcePath,
      targetAgentPattern: agent.name, // Target this specific agent
      targetPath,
      watchForChanges: false,
      debounceMs: 500,
      deleteOrphans,
      transferMode,
      isEnabled: true,
    })
  }

  const handleDeploy = (profile: DeploymentProfile) => {
    setDeployingProfile(profile.id)
    deployMutation.mutate(profile.id)
  }

  const formatDate = (date: string) => {
    return new Date(date).toLocaleString()
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FolderSync className="h-5 w-5 text-orbit-600" />
            File Sync Configuration
          </DialogTitle>
          <DialogDescription>
            Configure file synchronization for agent <span className="font-medium">{agent.name}</span>
          </DialogDescription>
        </DialogHeader>

        <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as 'profiles' | 'create')}>
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="profiles">
              Matching Profiles ({matchingProfiles.length})
            </TabsTrigger>
            <TabsTrigger value="create">
              <Plus className="h-4 w-4 mr-1" />
              Create New
            </TabsTrigger>
          </TabsList>

          <TabsContent value="profiles" className="mt-4 space-y-4">
            {isLoading ? (
              <div className="flex items-center justify-center py-8">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : matchingProfiles.length === 0 ? (
              <div className="text-center py-8 border rounded-lg">
                <FolderSync className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
                <p className="text-lg font-medium">No Matching Profiles</p>
                <p className="text-muted-foreground mb-4">
                  No deployment profiles target this agent
                </p>
                <Button onClick={() => setActiveTab('create')}>
                  <Plus className="h-4 w-4 mr-2" />
                  Create Profile
                </Button>
              </div>
            ) : (
              <div className="space-y-3">
                {matchingProfiles.map((profile) => (
                  <div
                    key={profile.id}
                    className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-2">
                          <h3 className="font-semibold truncate">{profile.name}</h3>
                          {profile.watchForChanges && (
                            <Badge variant="outline" className="text-xs">Auto-watch</Badge>
                          )}
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                          <div className="flex items-center gap-2">
                            <FolderInput className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                            <span className="text-muted-foreground">Source:</span>
                            <span className="font-mono text-xs truncate">{profile.sourcePath}</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <FolderOutput className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                            <span className="text-muted-foreground">Target:</span>
                            <span className="font-mono text-xs truncate">{profile.targetPath}</span>
                          </div>
                        </div>
                        {profile.lastDeployedAt && (
                          <p className="text-xs text-muted-foreground mt-2">
                            Last deployed: {formatDate(profile.lastDeployedAt)}
                          </p>
                        )}
                      </div>
                      <Button
                        size="sm"
                        onClick={() => handleDeploy(profile)}
                        disabled={deployingProfile === profile.id}
                      >
                        {deployingProfile === profile.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Play className="h-4 w-4" />
                        )}
                        <span className="ml-2">Deploy</span>
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </TabsContent>

          <TabsContent value="create" className="mt-4 space-y-4">
            <div className="bg-muted/50 rounded-lg p-4 mb-4">
              <div className="flex items-center gap-2 mb-2">
                <Server className="h-4 w-4 text-orbit-600" />
                <span className="text-sm font-medium">Target Agent</span>
              </div>
              <p className="text-sm">
                <span className="font-mono bg-muted px-2 py-0.5 rounded">{agent.name}</span>
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="sync-name">Profile Name *</Label>
              <Input
                id="sync-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="my-sync-profile"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="source-path">Source Path *</Label>
              <Input
                id="source-path"
                value={sourcePath}
                onChange={(e) => setSourcePath(e.target.value)}
                placeholder="C:\Deploy\MyApp"
              />
              <p className="text-xs text-muted-foreground">
                Local folder on the server containing files to sync
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="target-path">Target Path *</Label>
              <Input
                id="target-path"
                value={targetPath}
                onChange={(e) => setTargetPath(e.target.value)}
                placeholder="C:\inetpub\wwwroot\MyApp"
              />
              <p className="text-xs text-muted-foreground">
                Destination folder on the agent
              </p>
            </div>

            <div className="space-y-2">
              <Label>Transfer Mode</Label>
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

            <div className="flex items-center justify-between py-2">
              <div className="space-y-0.5">
                <Label>Delete Orphans</Label>
                <p className="text-xs text-muted-foreground">
                  Remove files on target that don't exist in source
                </p>
              </div>
              <Switch checked={deleteOrphans} onCheckedChange={setDeleteOrphans} />
            </div>

            <div className="pt-4">
              <Button
                onClick={handleCreate}
                disabled={createMutation.isPending}
                className="w-full"
              >
                {createMutation.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin mr-2" />
                ) : (
                  <Plus className="h-4 w-4 mr-2" />
                )}
                Create Sync Profile
              </Button>
            </div>
          </TabsContent>
        </Tabs>

        <DialogFooter className="mt-4">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
